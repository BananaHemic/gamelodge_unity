# Gamelodge
Gamelodge is a place to play fun games made by other users, and build games of your own, on desktop or in VR.

### Features
- Extremely bit-efficient networking
  - Roughly 2x the thoroughput of Photon
  - 65k object limit
  - Completely free of runtime memory deallocation
  - Because the network stack is so efficient, you can synchronize complex objects like ragdolls
- Save to a local file the entire networked game state as a recording
  - Great for making gameplay videos, you can re-watch what was happening from different perspectives
  - Incredibly useful for fixing bugs. Users can send devs the recording demonstrating what happened
  - Replay recordings with edited scripts to quickly verify that an issue has been fixed
- VR and Desktop supported
  - Switch back and forth at runtime via F2
- Multiplayer building
  - You can build games, write code, and test while people are playing
- Compile-free
  - All programming is done in MiniScript, a lua/python like language focused on simplicity
  - Although simple, the scripting is full-featured enough to create complex behaviors like the MuscleMover
    
### Limitations/Issues
- The desktop building controls are slow, buggy, and difficult. It needs to be rewritten
- The scripting API should be cleaned up, it's not clear how to call certain functions
- There are a decent number of known bugs, most from unanticipated mixtures of scripts, and from race conditions
- Downloading a new asset bundle can cause you to disconnect from the server
- Functions are currently in the global namespace, there should be a requirement to import functions
- There's no lobby and only one server room

# Compiling
We currently use Unity 2019.2.14f1 but later versions should work as well.

