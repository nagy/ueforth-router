{ pkgs ? import <nixpkgs> { }, nagy ? pkgs.nur.repos.nagy }:

rec {
  software = nagy.ueforth.fork.overrideAttrs ({ env ? { }, ... }: {
    env = env // {
      userwords_h = "${./userwords.h}";
      userwords_fs = "${./userwords.fs}";
    };
  });

  compiled = pkgs.runCommandLocal "compiled" {
    env.ARDUINO_DIRECTORIES_DATA = nagy.lib.mkArduinoInstalled;
    env.FQBN = "esp32:esp32:esp32s3:CDCOnBoot=cdc";
    nativeBuildInputs =
      [ pkgs.arduino-cli (pkgs.python3.withPackages (ps: [ ps.pyserial ])) ];
  } ''
    arduino-cli compile \
      --fqbn $FQBN \
      --output-dir out \
      ${software}/share/ueforth/ESP32forth/
    mv out $out
  '';

  uploadscript = pkgs.writeShellApplication {
    name = "uploadscript";
    runtimeInputs = compiled.nativeBuildInputs;
    runtimeEnv = {
      ARDUINO_DIRECTORIES_DATA = compiled.ARDUINO_DIRECTORIES_DATA;
      FQBN = compiled.FQBN;
      INPUT_DIR = compiled;
    };
    text = ''
      exec arduino-cli upload --fqbn $FQBN --input-dir $INPUT_DIR "$@"
    '';
  };
}
