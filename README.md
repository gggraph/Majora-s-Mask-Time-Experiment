# Majora's Mask Time Experiment

https://user-images.githubusercontent.com/62741099/188935677-b5ed6145-fdba-4524-b8b9-4e79a14f48b9.mp4

**Syncing Majora's Mask with your system clock and more...**

A software that can modify the clock system of The Legend Of Zelda - Majora's Mask. 

This game is known for the way it tell stories with its day and night cycle and invite the player to manipulate it. This software open the game's time mechanism to much more.

There is actually 3 core methods in the solution. You can choose one:

* Syncing game time with **computer time**: day and night hours are synced with the real world. Is it playable? don't know.
* Syncing game time with computer **battery time remaining**. 100% of battery is first day morning. Moon crash at 1% 
* Syncing game time with **CPU activity**. More your CPU is active, more time is consumed. Do not open too much softwares.

## How to make it work

You will have to get a **rom of Majora's Mask** and run it with an emulator : the only one which will work actually is **project64**.

Follow those steps:
* Copy the save file from this repository to the save folder of the emulator.
* Run the game in your emulator.
* Compile the project (or get the .exe in the build folder) and run it.
* Follow software instructions.
* Have fun.


## How it works

Because there is different version of the majora's mask rom and base pointer of the game in ram can change between emulators. I prefered to use a specific state of the game that have known values at known memory offsets. The software will force the emulator to load the save
then iterate through memory region of the processus finding addresses containing **16338** which is the time stored before the first day morning.
Once pointers of minute and day data have been found, we can just overwrite data at those address with anything we want.



