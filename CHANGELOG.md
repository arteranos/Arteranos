# Version 2.9.2

## Fixes and improvements

- Join Friend: Visible friends get a "Travel To" button in their list entry line.

# Version 2.9.1

## Fixes and improvements

- More reliable server discovery, especially in the case of occasional use of the Host mode
- Added Arteranos Prime as the hook enter the server network - faster startup
- (For self-build) Easier deployment and sha256sums file creation

# Version 2.9.0

## New features
- **World Editor**
  - Uses Altspace-like...
    - ...primitives (more than Altspace offered)
    - ...kits (creation **AND** usage)
    - ...world templates
  - ... glTF objects
  - 'Tool' objects and components like...
    - ... lights
    - ... spawn points
    - ... **in-world teleport markers and buttons**
    - ... Object dispenser (Object which spawn other objects click)
  - **Object hierarchy**
  - **Undo/Redo**
  - **Cut** (while deleting objects) **& Paste**
  - Keyboard & Mouse object movement in the desktop mode
  - Easier screenshot on world save
  - ...
- Linux Dedicated Server
  - Much lower system requirements - upcycle your old potato :)
- In-app file dialog
  - Easier way to use on-disk resources, like pictures

## Fixes and improvements

- **Network overhaul**
  - UDP instead of TCP - more resilient
  - **NAT/Firewall hole punching**
    - Fussing with firewall rules, port forwarding or your DSL router? Maybe not as much anymore.
- **UI rework**
  - Dark theme (easier on your eyes in VR)
  - Better control flow (using ESC/A button to go back, everytime)
  - ...
- IPFS backend moved to external process
  - Maintenance is outsourced
  - Better implementation, independent to the client itself - safer from stutter
- Installation packages improvements
  - Windows: Firewall exceptions for Arteranos and its embedded IPFS daemon
  - Linux: Debian packages (new)
- Grabbable/Kinematics synchronizastion
- Avatar pose propagaion
- Quick connect: Use last known good connection to establish a new connection
- Quick connect allows to try connecting to a server with uncertain online state
- Other small fixes and quality-of-life improvements

# Version 2.0.0

## New Features

- **Peer-to-peer data handling of avatars and worlds**
  - No need to host files externally to show your creation
- **Custom avatar uploading**
  - Download a (RPM) avatar .glb file, **modify as you like**, and use it in Arteranos.
- User icons
  - 2D icons in the friend list to make users more distinguishable
- Improved world hosting
  - Build your world as ZIP file, **keep it in your PC**, start the server or host mode, and let it change the current world to your creation.
- Full IPv6 support
  - Curious. Even AAA MMO's still connectto their servers via IPv4....
  - No need for NAT, port forwarding, and such hassles... Well, only if your ISP is up to date.
- Remote server administration
  - Better way to handle the dedicated server. Or to let your good(!) friend handle your server.


# Fixes and improvements

- Ready Player Me integration removed
  - Too brittle, stopped working too often, now requiring registration to make it work at all.
  - -> Just see in 'New Features'... :)
- Metadata microserver removed
  - Grew up into IPFS.
- Third party login removed
  - **Unneccessary**, with the passkey authentication.
- Transition scene in world change
  - Better progress indication. Server discovery, server connection, world downloading.
- Improved avatar pose and animation handling
- Keyboard/Mouse movement improvement: WASD+QE (turning/strafing)
- **Many internal upgrades**
  - ... And I mean, _many_. The migration to the peer-to-peer data management took me A LOT. Worlds, Avatars, Icons, Server data, User data... like, everything.

# Version 1.4.0

## New Features

- Configurable body height
- Server collection and propagation - public servers say: Spread the word!
- Server search and discovery: Travel to the world you want - any suitable server!
- World list UI: Larger panels, more info. Popular? Where are your friends? There they are!
- Dedicated server gets a dedicated configuration directory.
- VR: Movement - Turn/Strafe separately for the left and right controller
- VR: Controller - On/Off. No 'zombie' arms, no hands right in your face just because of idle controllers.

## Fixes and improvements

- Info panel: Displaying the launcher link and providing copy&paste (e.g. for Discord)
- Grabbable virtual keyboard
- Better download progress indicator
- More compact server data transfer: Less bandwidth use
- World metadata improvements
- Better handling of VR mode
- Legalese window, only when necessary. Bleargh...

# Version 1.0.0

## New Features

- User Heads Up Display (HUD) doubling as a quick menu
  - Size and position configurable 
- Emojis pane - sixteen emojis to display for you
- Passwordless authentication
  - Secret key in a file in your device
  - No password eavesdropping!
  - Public key to identify your friends across the servers
- Text messages button in the 'People' panel (if the receiver wants it to)
- **Flying!**
- Privacy panel: Hiding the user ID, or blocking text messages for strangers, and more
- Camera drone. Place it, or toss it high up, and snap a photo.
- Microserver for providing web links. Place a link in Discord to invite user into your server
- Kicking and banning users (if the user has sufficient privileges)
- Controller configuration
  - Disabling controllers let your avatar's hand be at rest - not in your face!

## Fixes and improvements

- Merging the voice and the world server -- one less port to be opened.
- User privileges and capabilities
- Massive improvements to authentication
- Nameplate improvements
- Virtual keyboard improvements
- VR look & feel improvements
- Various stability and bug fixes

# Version 0.5.0

## New Features

- **Offline scene - dedicated scene to stretch your (metaphorical) legs and to make yourself comfortable.**
  - You _can_ switch to the host mode for a quick meeting, though. :)
- Additional virtual keyboard layout: en_US (full)
- New teleport mode: Blink - Fade out, move, Fade in.

## Fixes

- Fixes a bug where the avatar occasionally has misplaced to (0,0,0), not the designated spawn point(s).

# Version 0.4.0

 **Breaking change - not backwards compatible prior to this version**

## New features

- People Panel: Incoming and outgoing friend requests - keep track of your outstanding friend requests
- Text messaging - send messages to online users.
  - Full end-to-end encryption, no snooping!
  - Online status setting - if you don't like to be disturbed, the messages will put on hold when you change your mind.
- Virtual Keyboard support
  - Always, VR only, never (if you can blind type with wearing a VR headset...)
  - Additional keyboard layouts? More to be added soon
- Control configuration
  - Separately left/right (in VR) configuration
  - Completely switching off the individual controller
  - Ray type: Straight line, Arc
  - Ray visibility: Always (red/white), only valid (white)
- Movement configuration
  - Snap turn / Smooth turn (slow, fast, as you like)
  - Instant teleport / Sliding "Zipline" teleport (the same...)

**Finally, the system menu panels are filled, no more "Reserved for future use"  placeholders!**

## Fixes

- People Panel: Hide buttons, not just disable them.

# Version 0.3.0

 **Breaking change - not backwards compatible prior to this version**

## New features

- Embedded avatar creation
- Avatar gallery to store and retrieve your avatars
- Nameplates
  - Button for muting/unmuting
  - Button for blocking
  - Offering and accepting friendships
- User lists
  - Online in the current server
  - Friends
  - Blocked users
- Privacy bubble, with adjustable size
  - For friends
  - For strangers
  
## Fixes

- Added Network Authenticator for access control and version check


# Version 0.2.0

## New features

- Browser integration
  - Starting Arteranos just by clicking a link in web page
  - Link provides the server address and/or the targeted world.
    - **Good for publishing, like for an event's contact details**
- **Setup wizard**
- Audio system menu pane functional
  - Master/Voice/Environment volume sliders
  - Microphone device selection
  - Microphone gain slider
  - AGC (Automatic Gain Control) to adaptively adjust the microphone gain

## Fixes

- Major code cleanup
- Addad a notification for failed server connection
- UPnP NAT traversal now on-demand
  - Now you can use the dedicated server _and_ the desktop client at the same time.
- New server entry improved - a simple server like 'localhost' should suffice in the default cases

# Version 0.1.0

## New features

- Core functionality
  - OpenXR capability
  - Server running
  - Client running
  - Loading Worlds
  - Connecting to remote servers
  - Voice chat
  - Avatar selection
  - Client & Server preferences
  