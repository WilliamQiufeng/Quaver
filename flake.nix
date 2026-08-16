{
  description = "Quaver Dev Environment";

  inputs = {
    self.submodules = true;
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
    flake-parts.url = "github:hercules-ci/flake-parts";
    rust-overlay.url = "github:oxalica/rust-overlay";
    quaver-scripting-native = {
      url = ./Quaver.Scripting.Native;
      inputs.nixpkgs.follows = "nixpkgs";
      inputs.flake-parts.follows = "flake-parts";
      inputs.rust-overlay.follows = "rust-overlay";
    };
  };

  outputs = inputs:
    inputs.flake-parts.lib.mkFlake { inherit inputs; } {
      imports = [
        inputs.quaver-scripting-native.flakeModule
      ];

      systems = [ "x86_64-linux" ];

      perSystem = { self', lib, system, ... }:
        let
          pkgs = import inputs.nixpkgs {
            inherit system;
            config.allowUnfree = true;
            overlays = [
              (import inputs.rust-overlay)
            ];
          };

          quaverLibs = with pkgs; [
            stdenv.cc.cc.lib
            zlib
            icu
            openssl
            xorg.libX11
            xorg.libXi
            xorg.libXext
            xorg.libXrandr
            xorg.libXcursor
            glib
            steam
            SDL2
            libGL
            libglvnd
            mesa
            vulkan-loader
            alsa-lib
            libpulseaudio
            wayland
            wayland-protocols
            libdecor
            libxkbcommon
            dbus
          ];

          quaverLibraryPath = lib.makeLibraryPath quaverLibs;
          openglDriverPath = "/run/opengl-driver/lib:/run/opengl-driver-32/lib";
          dotnetSdk = pkgs.dotnetCorePackages.combinePackages [
            pkgs.dotnet-sdk_10
            pkgs.dotnetCorePackages.sdk_8_0
          ];
          nativeRustToolchain = pkgs.rust-bin.selectLatestNightlyWith
            (toolchain: toolchain.default);
          nativeRustBuildDeps = with pkgs; [
            pkg-config
            rustPlatform.bindgenHook
          ];
        in {
          _module.args.pkgs = pkgs;

          packages.default = self'.packages.quaver-scripting-native;

          devShells.default = pkgs.mkShell {
            nativeBuildInputs = [
              dotnetSdk
              nativeRustToolchain
            ] ++ nativeRustBuildDeps;

            shellHook = ''
              export NIX_LD_LIBRARY_PATH=${quaverLibraryPath}:${openglDriverPath}:$NIX_LD_LIBRARY_PATH
              export DOTNET_ROOT=${dotnetSdk}/share/dotnet
              export DOTNET_ROLL_FORWARD="LatestMajor"
              export RUST_SRC_PATH=${pkgs.rustPlatform.rustLibSrc}
              export SDL_VIDEO_X11_WMCLASS=Quaver
              export NIX_LD_LIBRARY_PATH=$NIX_LD_LIBRARY_PATH:$PWD/Quaver.Shared
              export LD_LIBRARY_PATH=${quaverLibraryPath}:${openglDriverPath}:$LD_LIBRARY_PATH:$PWD/Quaver.Shared
            '';
          };
        };
    };
}
