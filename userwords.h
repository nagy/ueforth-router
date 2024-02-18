#include <stdint.h>
/* #include <esp32-hal-rgb-led.h> */

#define USER_VOCABULARIES V(user)
#define USER_WORDS \
  XV(internals, "user-source", USER_SOURCE, \
      PUSH user_source; PUSH sizeof(user_source) - 1) \
  XV(user, "getpid", GETPID, PUSH getpid())

  /* XV(user, "neopixel-write", NEOPIXEL_WRITE, neopixelWrite(RGB_BUILTIN, n2, n1, n0);DROPn(3)) \ */
