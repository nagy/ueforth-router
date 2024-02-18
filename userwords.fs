vocabulary user user definitions
also sockets
\ also streams
\ cannot use "also streams" for now because the ">stream" word always returns and does not wait
also internals \ for cmove
transfer user-builtins

variable my-id-var -1 my-id-var !

\ ## The Route Table
\ Place a list of xt's on this table.
\ Each of them must a the stack effect ( a n -- a n )
\ They are allowed to modify the passed buffer
\ and the response will be routed back.

\ Maximum size for route table. Could be reduced if needed.
16 constant max-route-entries
create route-table max-route-entries 3 cells * allot
( for esp clear out ) route-table max-route-entries 3 cells * erase
: route-offset ( n -- )    3 cells * route-table  + ;
: route->from! ( n n -- )  route-offset 0 cells + ! ;
: route->to!   ( n n -- )  route-offset 1 cells + ! ;
: route->xt!   ( xt n -- ) route-offset 2 cells + ! ;
: route->from@ ( n -- n )  route-offset 0 cells + @ ;
: route->to@   ( n -- n )  route-offset 1 cells + @ ;
: route->xt@   ( n -- n )  route-offset 2 cells + @ ;
: route->! ( from to xt n -- )
  { from to xt n }
  from n route->from!
  to n route->to!
  xt n route->xt! ;
: route1. ( n -- ) { r }
  r . ." :  "
  r route->from@ . ." - "
  r route->to@ .
  r route->xt@ dup 0= if
    drop ." _"
  else
    see.
  then ;
: route. ( -- )
  max-route-entries 0 do
    i route1. cr
  loop ;

defer fallback
' pause is fallback

: route-xt ( n -- xt ) { callnum }
  ['] fallback \ nothing to do
  max-route-entries 0 do
    callnum i route->from@ >= if
      callnum i route->to@ <= if
        drop i route->xt@
        \ ." found: " i route->xt@ see.
        leave
      then
    then
  loop ;

\ ## Messages

create msg-len cell allot 0 msg-len ! \ erase needed for esp32
1024 constant msg-len-max
create msg-data msg-len-max allot
: msg? ( -- n ) msg-len @ 0 > ;
: msg->to@ ( -- n ) msg-data ul@ ;
: msg->to! ( n -- ) msg-data l! ;
: msg->from@ ( -- n ) msg-data 4 + ul@ ;
: msg->from! ( n -- ) msg-data 4 + l! ;
: msg-swapaddr ( -- ) msg->from@ msg->to@ msg->from! msg->to! ;
: msg-payload ( -- a n ) msg-data 8 + msg-len @ 8 - ;
: msg. ( -- )
  ." MSG: len: " msg-len @ .
  ." to: " msg->to@ .
  ." from: " msg->from@ .
  ." xt: " msg->to@ route-xt see.
  cr ;
: clear-msg 0 msg-len ! ;
' clear-msg is fallback

sockaddr received
variable received-len sizeof(sockaddr_in) received-len !

\ ## Init
sockaddr incoming
DEFINED? ESP32-S3? [IF]
\ ESP_READ_MAC my-id-var ! \ TODO
also wifi
also wdts
also sockets
also ESP
also espnow
: user-init ( -- )
  wifi_mode_sta wifi.mode
  wifiram drop
  wifisetpsnone drop
  r| also wifi also wdts also sockets also esp also espnow | evaluate
  r| espnow_init drop | evaluate
  r| espbroadcast espnow_add_peer drop | evaluate
  r| ' espnow-receiver espnow_register_recv_cb drop | evaluate
  r| z" my-wifi-ssid" 0 wifi.begin | evaluate
  r| esp_read_mac >r drop drop drop drop drop r> | evaluate
  r| 24 lshift $0003a8c0 or | evaluate
  r| $0103a8c0 $00ffffff 0 wifi.config | evaluate
  r| 1 incoming ->port! | evaluate
  r| esp_read_mac >r drop drop drop drop drop r> my-id-var ! | evaluate
  r| my-id-var @ 100000 + my-id-var @ 100000 + ' evaluate-wrapped 0 route->! | evaluate
  r| my-id-var @ 100 = [if] 5000 ms wifi.status 3 = [if] [else] bye [then] [then] | evaluate \ if not WL_CONNECTED, restart
  r| my-id-var @ 100 = [if] hear-init [then] | evaluate
  r| my-id-var @ 100 = [if] 100 99999 ' udp-to-from 9 route->! [else] 100 99999 ' espnow-to-target 9 route->! [then] | evaluate
  r| route. | evaluate
  r| route-loop | evaluate
;
[ELSE]
\ posix
getpid my-id-var !
: user-init ( -- )
  0 echo !
  also user
  \ getpid my-id-var !
  $0100007F incoming ->addr!
  2048 incoming ->port!
  r| my-id-var @ my-id-var @ ' evaluate-wrapped 0 route->! | evaluate
  \ r| hear-init | evaluate
  \ r| route-loop | evaluate
;
[THEN]

\ ## UDP Network
-1 value sockfd
: udp-reader ( -- )
  begin
    sockfd msg-data msg-len-max 0 received received-len recvfrom
    dup -1 <> if
      received ->port@ msg->from! \ so the udp sender does not have to set this.
      msg-len !
    else drop then
    pause
  again ;
' udp-reader 10 10 task udp-reader-task
udp-reader-task start-task
: udp-init ( -- )
  AF_INET SOCK_DGRAM 0 socket to sockfd
  sockfd non-block throw
  sockfd incoming sizeof(sockaddr_in) bind throw
  -1 throw ;
: hear-init ( -- ) begin 1000 ms ['] udp-init catch -1 = until ;

: route-loop ( -- )
  begin
    msg? if
      msg.
      msg->to@ route-xt execute
    then
    pause
  again ;

sockaddr fromudp
: udp-to-from ( -- )
  received ->addr@ fromudp ->addr!
  msg->to@ fromudp ->port!
  sockfd msg-data msg-len @ 1024 min 0 fromudp sizeof(sockaddr_in) sendto drop
  0 msg-len ! ;

\ ## Evaluator
create outmsg-len cell allot \ 0 outmsg-len !
create outmsg-data 10 msg-len-max * allot
: outmsg-len+= ( n -- ) outmsg-len @ + outmsg-len ! ;
: outmsg ( -- a n ) outmsg-data outmsg-len @ ;
: outmsg? ( -- n ) outmsg-len @ 0 > ;

\ types into msgout buffer instead of on the terminal
: resp-type ( a n -- )
  2dup \ debug \ more than just debug :(
  outmsg + swap cmove
  dup outmsg-len+=
  2drop \ default-type \ debug
;

: evaluate-wrapped ( -- )
  ['] resp-type is type
  0 outmsg-len !
  msg-payload ['] evaluate catch drop
  ['] default-type is type
  msg-swapaddr \ answer to requester
  outmsg-len @ msg-len-max min >r
  \ ." MAXMSG " r@ cr
  outmsg-data msg-payload drop r@ cmove
  8 r@ + msg-len !
  r> drop ;

: espnow-receiver { len data mac }
  data msg-data len cmove
  len msg-len ! ;

create espbroadcast $ff c, $ff c, $ff c, $ff c, $ff c, $ff c,
variable esptarget espbroadcast esptarget !

\ ## Register Routes
\ my-id-var @ 100000 +
\   my-id-var @ 100000 + ' evaluate-wrapped 0 route->!

DEFINED? ESP32-S3? [IF]
\ fallback route for deeper nodes to find back to home
: espnow-to-target ( -- )
  esptarget @
    msg-data msg-len @ ESP_NOW_MAX_DATA_LEN min
    espnow_send
    drop
  0 msg-len ! ;
\ 1 1 ' espnow-to-broadcast 1 route->!
[THEN]

only forth definitions
