# Bones Folder (alpha)

Adds a Bones folder to the game's sync directory which gets filled with the bones of runs that end too soon, so that they might have one last shot at the moon.

Alpha test for what amounts to a prototype. Requires at least beta version 2.0.211.36 (won't work on main, won't work on lang).

### Content Warnings

__If you suffer from photosensitivity, please be advised that the mod has a few flashing visual effects that can be disabled or greatly slowed with a non-debug option in the options menu.__

### Resources

Use [this page](https://wiki.cavesofqud.com/wiki/File_locations) to get platform specific *default* locaitons.

### Installing the Mod

Place a copy of, or make a symlink to, this project's "Mod" directory in your mods directroy, rename it something less generic (eg. `Bones_Folder_Mod` or `UD_Bones_Folder`).

"Offline mods" on the linked page refers to the "mods directory".

Alternatively, if you have the game on steam, there's an [unlisted workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3693957923) you can subscribe to.

### Using the "Test Bones"

If one doesn't already exist, create a directory called "Bones" in either of the following directories, but preferable the first:
- "[path to game]\Freehold Games\CavesOfQud\Synced"
- "[path to game]\Freehold Games\CavesOfQud"

"Configuration files" on the linked page refers to the second list item above.

Unzip the contents of [Bones.zip](Bones.zip) into the above "Bones" directory.

Assuming no other bones and in descending date order, your Sync folder will look approx. like the below:

	Synced
	├── Bones
	│	├── 1feedcc3-03ee-4053-a986-70af5df9716e
	│	├── 29d120aa-7906-4eae-8587-90c18867bcb2
	│	├── dfb40429-cd95-4631-bb00-d1dce266f79c
	│	└── a66f78dc-0e31-4f85-9048-2cd41c9c9ea7
	└── Saves

### Wishes

`make bones` - does what's on the tin. Makes a bones out of your current character.

`make bones die` - also does what's on the tin. Makes you die to make a bones (outside of thrown exceptions, this *will* kill your character, only the "sure you want to die?" debug option will prevent it)

`cremate bones` - does the same thing as the bones menu "cremate all" option. Hold tab or type "CREMATE" to delete *all* bones.

`go2bones` - takes you to the current run's pending bones, or tells there aren't any if there aren't any.

## Notes

### Options
Once you load up the game, and go into the options and give the debug options for this mod a quick read and decide if any of the make sense for the testing you'd like to do.

### Disparate Mod Configurations
Clicking on a bones file in the Bones menu will list the mods that are different between the current configuration and that of the bones file, and gives the option of reloading the game with either the bones file's mods added to the curent configuration or the current configuration replaced with that of the bones file.

If a mod is outright unavailable (because it's not even install), that will be mentioned.