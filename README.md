# Arteranos

**This is the core image, ready to make a home brew. If you prefer a ready-made package for a quick start, you may want to look at the [Arteranos Loader](https://github.com/arteranos/Arteranos-Loader) and it's most recent release.**

## System requirements

- Windows 10/11, 64-Bit.
  - It's designed to be architecture agnostic, so more recent versions of Windows should likely work.
- Linux (confirmed working on Ubuntu 24)
  - *Desktop only*, VR support in development

- OpenXR-supported tethered headset, _if applicable_<sup>*</sup>
  - ~~Oculus~~ Meta Quest 2, using Oculus Link (both Air and cable)
  - Meta Quest 2, Virtual Desktop
  - Meta Quest 2, Steam Link
  
- For building you need:
  - **Unity 6** with the installed modules for the desired target platform

<sup>*</sup>) This application both supports VR and 2D (aka Desktop) mode, and it's intended to smoothly switch the modes back and forth, _anytime_.

## Quick Building (Windows 64)
 1. Download the source using git: `git clone --recurse-submodules https://github.com/arteranos/Arteranos.git`

 2. Open the Arteranos as the Unity project (adding Arteranos's directory in Unity Hub, then open it)
 3. In Unity's menu, there's a custom menu heading named `Arteranos`. From there, you can build the respective directories and tarballs.
 4. Using the loader (see link above), you can set up the necessary IPFS node before to start up Arteranos itself right from the `build/<edition>-<os>-amd64`  directory.

### Now what _are_ these apps?
`Arteranos.exe` is the full-fledged client. You can use it in the desktop mode or VR, as you like, both for connecting to a server, or to host your own server, with or without your presence in the world you set up.

`Arteranos-Server.exe` is the dedicated server. It won't show anything, and it's intended to persist in the backgound and to consume minimal resouces and to be connected to from other's clients, maybe you.

### And now?

You can jump into the deep end of the pool with starting Arteranos, or just read further a bit :)

For those who look before leaping, you can [browse the documentation](https://arteranos.gitbook.io/arteranos-documentation) (in the works...)
The documentation is meant both to be read end-to-end, and for looking up the needed information.

## Getting in Touch
To report issues and feature requests: [Github issues page](https://github.com/willneedit/Arteranos/issues).

To chat with the team and other users: join the [Arteranos Development Discussion](https://discord.gg/jHYFFd78B9).

---
## License

This product is copyright 2023 by willneedit and licensed under the [Mozilla Public License 2.0](LICENSE.md).

This package contains third-party software components, owned and licensed by [this list](Third%20Party%20Notices.md).
