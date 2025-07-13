# Floof Station

<p align="center"><img src="https://raw.githubusercontent.com/Fansana/floofstation1/master/Resources/Textures/Logo/flooflogo.png" width="512px" /></p>

---

Floof Station is a whitelist-only 18+ Medium Roleplay furry-oriented server (ERP enabled), of the game [Space Station 14](https://spacestation14.com/). Anybody interested in checking us out or joining us, can apply for membership in our Discord linked down below.

Floof Station is a relaxing environment offering slow-paced, admin-driven events where members can develop their stories. The focus is on building meaningful interpersonal relationships, from friendships to romantic connections. This includes erotic roleplay, allowing members to explore and bring their wildest fantasies to life, all within a framework of mutual respect and consent.

Floof Station is a fork of [Einstein-Engines](https://github.com/Simple-Station/Einstein-Engines).

## Links

[Steam (WizDen Launcher)](https://store.steampowered.com/app/1255460/Space_Station_14/) (NOTE: in order to see us on the Hub, you will have to opt-in seeing 18+ servers in the filters!)

[Discord](https://discord.com/invite/floofstation) (NOTE: in order to access to the rest of the Discord, you will have to be whitelisted first!)

[Wiki](https://wiki.floofstation.com/index.php/Main_Page) (NOTE: you will need a SS14 account in order to access the Wiki!)

[Online Cookbook](https://heurl.in/ss14/recipes?fork=floof) (kindly provided by the wonderful Arimah <3)


## Contributing

We are happy to accept contributions from anybody, come join our Discord if you want to help!
We got a [list of issues](https://github.com/Fansana/floofstation1/issues) that need to be dealt with, which anybody interested is free to try and sort out. Don't be afraid to ask for help in the Discord if you need any!

## Building

Refer to [the Space Wizards' guide](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) on setting up a development environment and for general information. But do keep in mind that Einstein Engines, the codebase Floof Station is based on, is an alternative codebase to the base one provided by WizDen, and many things may thus not apply nor be the same.
We provide some scripts shown below to make the job easier.

### Build dependencies

> - Git
> - .NET SDK 9.0.100


### Windows

> 1. Clone this repository
> 2. Run `git submodule update --init --recursive` in a terminal to download the engine
> 3. Run `Scripts/bat/buildAllDebug.bat` after making any changes to the source
> 4. Run `Scripts/bat/runQuickAll.bat` to launch the client and the server
> 5. Connect to localhost in the client and play

### Linux

> 1. Clone this repository
> 2. Run `git submodule update --init --recursive` in a terminal to download the engine
> 3. Run `Scripts/sh/buildAllDebug.sh` after making any changes to the source
> 4. Run `Scripts/sh/runQuickAll.sh` to launch the client and the server
> 5. Connect to localhost in the client and play

### MacOS

> I don't know anybody using MacOS to test this, but it's probably roughly the same steps as Linux

## License

Please read the [LEGAL.md](./LEGAL.md) file for information on the licenses of the code and assets in this repository.
