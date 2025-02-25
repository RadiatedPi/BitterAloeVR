SQL4Unity Example - Read Me.

Before running the SQL4Unity Example you must do the following

In the Unity Editor
Move the folder SQL4Unity/Example/StreamingAssets to Assets/
If your project already has an Assets/StreamingAssets folder move the contents of SQL4Unity/Example/StreamingAssets to Assets/StreamingAssets

Open the scene SQL4Unity/Example/Example

Press Play.

To show data persistance Press Play to stop the scene before the objects have reached their final position.

Press Play and the objects will start again from their last position.

---------------------------------------------------------------------

To reset the database to its initial state

Disable the gameobject Example in the scene
Enable the gameobject Initialise Database in the scene
Press Play

The Unity console will show the output.

Press Play to stop the scene
Disable the gameobject Initialise Database in the scene
Enable the gameobject Example in the scene

The Example can now be run again.

---------------------------------------------------------------------

To examine the example MyGameDB database structure
Start the SQLWorkbench (Tools->SQL for Unity->Workbench)
Press 'Open Schema'
Navigate to Assets/SSSoftware/SQL4Unity/Example/Resources
Select Schema.asset

If the example scene has been run in the editor select the database MyGameDB then enter the command

Update Players set CurrPos=null

in the SQL Command text area and press Execute.

The example can then be run again from its initial state

To examine the example MyGameDB data
Start the SQL Table Editor (Tools->SQL for Unity->Table Editor)
Press 'Open Database'
Select MyGameDB.s4u and press OK

SteveSmith.SoftWare 2018.

SQL4Unity V.1.0.3 - February 2021

The code file SQL4Unity/Example/Scripts/SQLExample.cs has been updated to show the usage of the new Stored SQL and Parameter substitution features.
I have left the original code in the code file (commented out) for reference purposes only
The functionality is identical to that from V.1.0.2
