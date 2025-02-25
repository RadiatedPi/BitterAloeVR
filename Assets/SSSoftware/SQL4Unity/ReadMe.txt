SQL4Unity - Read Me.

SQL4Unity can be accessed under the Unity Menu Tools->SQL for Unity

Please read Tools->SQL for Unity->Help Installation first.

For the example please read Assets/SSSoftware/SQL4Unity/Example/ReadMe first.

For the latest information on bugs, fixes and workrounds please refer to the Contact page at https://stevesmith.software

SteveSmith.Software


Release Notes:

	V.1.0.8 November 2024
		Bug fix for Table Editor GUI Foldout error.

	V.1.0.7 March 2024
		Bug fix for SQLExecute constructor when not called on the main thread. See documentation for details.

	V.1.0.6 October 2023
		Bug fix in Table Editor after updating database.

	V.1.0.6 August 2023
		Fixes for problems with Unity 2022.3 LTS
		Bug Fix. Allow for zero length strings
		Enhancement
			SQL4UnityCLI
			Added Export and Import commands

	V.1.0.6 April 2023
		Bug Fix. Remove unnecessary #if from generated scripts

	V.1.0.6 December 2022
		Bug Fix. Parsing Color32 values in Update
			 Error processing Timestamp values in Import

	V.1.0.6 November 2022
		Removed 'Not Null' restriction from bool datatype.

	V.1.0.6 October 2022
		Bug Fix
			'Select count(*) from table' on empty table causes exception.
			Serializing null strings causes exception.
			Initializing SQLExecute in class declaration causes exception.
			Build error using IL2CPP and minimal code stripping
		Enhancement
			Added MySQL to Server namespace
				Allows for the direct connection to a MySQL database
			Auto-increment columns can be specified in Insert statements in order to preserve their original values,
			Export and Import have been changed to reflect this
			Added '!in' (Not In) and 'like' (for string columns) operands to the where clause. See documentation for details.

	V.1.0.5 July 2022
		Enhancement
			SQL Table Editor
				Allow opening of databases outside of the current project.

	V.1.0.5	April 2022
		Bug Fix
			Using NULL values in Constraint columns causes Illegal Cast Exception
			Float values truncated to 2 decimals in Table Editor
			Color and Color32 values incorrectly validated in Table Editor
			Compact Schema leaves orphaned Indexes
			Fixes Implemented

	V.1.0.5 April 2022
		Bug Fix
			Multiple OR clauses cause out of bounds error

		New Features
			Added ulong (UInt64) datatype
			Added sbyte (signed byte) datatype
			INSERT statement now returns the Inserted row, to facilitate the retrieval of auto-increment values

	V.1.0.5 March 2022
		Bug Fix
			SQLHelp not working in Unity 2020 or greater.
			Fallback to browser implemented.

			Using a Turkish language system corupts the schema.
			Fix implemented.

	V.1.0.5 February 2022
		New Features
			SQLWorkbench
			SQLEngine
				Added Key Constraints
					Constraints can be enforced on Insert and Update of a foreign key
					Update and/or Delete can be cascaded from the primary key to the foreign key

	V.1.0.4 January 2022
		New Features
			SQLWorkbench
				Generated classes are now defined as partial

			SQLExecute
				2 new methods added
				To allow Inserting multiple rows in one go.

				Command(string, SQLResult, SQLRow[])
				Command(string, SQLResult, List<SQLRow>) 

				For Development Builds ONLY
					SQLExecute("Database Name");
					SQLExecute.Open("Database Name");
				will now check for an updated database and will replace an existing one if found
			
			SQLPreBuild
				When building for Stand Alone SQLPreBuild will delete any existing database(s) in Application.persistentData


	V.1.0.4 December 2021
		Code base merged from SQL4Unity Client/Server V.1.0.4
		Re-write of documentation

	V.1.0.3 November 2021
		New features.
			SQL4UnityCLI.
				A Windows Command Line program to allow database usage external to Unity. Requires .Net Framework 4.7.1
				See documentation for full usage.
			SQLExecute.
				Added method ReplaceIfNewer which facilitates the replacement of an existing database with a newer version.
				Optionally copies the data from the old database to the new one.
			Blob datatype.
				Blob now accepts a Unity Texture2D as a parameter.
			DataTime datatype.
				DateTime now accepts a TimeSpan as a parameter
			SQL Data Export.
				The database export function of SQLWorkbench and SQLTableEditor now accept a .xml file extension.
				When .xml is used the data will be exported to a formatted XML file.
			SQLTableEditor.
				Table data is now paged in the table editor allowing quick and easy access to large tables.

		Bug Fix.
			28 May
				Error Serializing/Deserializing Multiple column Indexes.
			1 June
				Error validating Time Portion of a Data Time
			29 September
				Null values incorrectly accepted when using the SQLRow option of Insert
			11 November
				Accessing a Closed database throws an Exception.

		Known Issues.
			Android builds using Unity 2021.2.2 and IL2CPP result in a Deserialization error of the Sys table
			Workround: Use Mono backend when targeting Android with this Unity Version


	V.1.0.3 April 2021
		Bug Fix.
			2 April:
				Error Opening a partitioned Table.
				Error Updating Indexed Column
			4 April
				Incorrect partitioning of Index for Partitioned table
			7 April
				Error on String Index when using alphabets from a mixture of cultures.
					NOTE: SQL4Unity uses the C# InvariantCulture and Ordinal for string compares, This will be reflected in ORDER BY and GROUP BY results.

				Improved Error Logging
					In the event of an unexpected error a .log file is now written to Application.persistentDataPath with one of the following names
						SQL4Unity
						SQLWorkbench
						SQLEngine
						SQLParse
					To reflect where the error occured.
					Please refer to the Unity documentation for the exact location of Application.persistentDataPath in your installation.
					If an SQL4Unity error occurs the variable SQL4Unity.Utility.HasError will be set to TRUE;
					To prevent any further processing after an error has occured the variable SQL4Unity.Utility.StopOnError can be set to TRUE during setup.

					Should you wish to add your own error logging the following may be used
						SQL4Unity.ErrMsg.Log(string message);
						SQL4Unity.ErrMsg.Log(string[] messages);
						This will also output the message to the Unity Console

						The error messages can then be written to the .log file by calling
						SQL4Unity.Utility.WriteErrorLog(string path);
							Note: path may include a full path and file name or just a file name. It should not contain a file extension as .log will be appended.
							If no path is included the output will be written to Application.persistentDataPath
							If no file name is used the default (SQL4Unity) will be used

				Error on Parameter substitution on recuuring use of Stored SQL
					Multiple executes of a stored SQL will reslt in corruption of parameter data. The database must be recreated to reset the stored SQL to its correct settings.


    V.1.0.3 March 2021

		Bug Fixes for the following:
			SQLWorkBench V.1.0.6
				Export Schema - Missing space in CREATE TABLE before (
				Import Schema - CREATE PROCEDURE - SQL Statement not being imported correctly
				Create Database fails if SQL4Unity/Scripts folder does not exist
			SQLParse V.1.0.4
				Incomplete parsing of Select statement for ORDER and GROUP BY clauses.
				Float and double columns parsing incorrectly for non us-en cultures.
			Table Editor V.1.0.4
				Float and double columns parsing incorrectly.
				Default values not being applied to columns left blank or NULL.
			DataType.ToString()
				Incorrect formating of floats and doubles when value > 999.99
				Automatically replace ' (single quote) with ` (back tick) in string values to mitigate parse errors
				
    V.1.0.3 February 2021

		This version contains many new features but is completely backwards compatible with version 1.0.2 so no update script is required.
		Some of the features may only take effect on newly defined/created databases.

		Note: This release is for Unity 2018.4 and newer. Unity versions lower than 2018.4 are no longer supported.
		The Dll's are now compiled using .Net Framework 4.7.1 and so your Unity projects must use the .Net 4.x as Scripting Runtime and Api Compatibility Level

		Please refer to the full documentation at https://stevesmith.software for more detailed information on the changes shown below.

		SQLWorkBench V.1.0.5
			Added new data types
				Blob - For storing byte[] such as texture and image/sprite data
				ushort, uint
			Added new Index type
				List - this can be used to index columns of data type ushort and uint and will be the default index type for rowid's and autoincremented primary keys.
					It sohuld only be used for columns containging mainly sequential numbers as, although it is very fast, it also occupies a little more memory than Dict.
			Added support for very large tables
				The previous limit of the number of rows in a table was 64k. This has now been increased to 2b. When option is selected the table data and it's indexes will
				be partitioned into 8k chunks. It is advisable to use this option for all tables containing > 10k rows and it is required if the table is to contain > 64k rows.
			Added Stored SQL's
				SQL Statements can now be stored in the database. This will speed up execution time as the parsing stage of the sql command process is now done at design time
				rather than at run time.
			Added optional Database Password protection
				Please note: SQL4Unity does not store the actual password used so if you lose/forget the password you lose access to your data.
			Modified Database->Options->Import
				AutoCommit is now off by default to speed up the Import process
				Added an optional command for Import files
					SET COMMIT
					to set an immediate commit point
					or
					SET COMMIT n
					to commit automatically after n rows
					So SET COMMIT 5000 will commit after every 5000 rows of the import file

		SQLExecute
			Addded Parameter substitution in SQL commands
			Added the 'Execute' statement for running stored SQL's
			Added Database password option when opening a database in the form "DatabaseName#Password"
			Improved performance on indexes
			Fixed Deserialising error on MacOS

		SQL Table Editor
			Added Database password prompt for password protected databases

		SQL4Unity/Plugins/SQL4UnityServer.dll
			This new dll allows direct connection with a Microsoft SQL*Server database to enable syncronisation between a local SQL4Unity database and a remote server

	V.1.0.2 April 2020

		This version contains many changes, please backup your project before upgrading.
		For more information on any of the changes specified below please refer to the SQL for Unity documentation

		Tools -> SQL for Unity -> Update to V.1.0.2
			Menu option to update an existing project to this version
			The menu option and the associated script (SQL4Unity/Editor/SQLUpdate.cs) will be removed 
			automatically once the update has run or if no update is required for the project.
			If an update is required the other menu options are disabled until the update has run to avoid
			causing problems.

		SQLWorkBench V.1.0.4
			Added Import and Database -> Export Schema
			Added Database -> Refresh Resources
			Added new data types
				Vector2Int, Vector3Int, RectInt
			
			Changed the format of automatically generated scripts.
			Important: Any existing automatically generated database scripts MUST be regenerated
			before using this version.

		SQL Table Editor
			Added Refresh Resources option

		Tools -> SQL for Unity -> Refresh Resources
			New Menu option to update Resources references in databases

		SQL4Unity/Editor/SQLPreBuild.cs
			Optional script to refresh Resources in databases at Build time

		SQLResult
			Added object[] GetColumn(int column index) or (string column name)
			Added T[] GetColumn(int column index) or (string column name)

		SQLExecute
			Added bool Command(string query, SQLResult result, SQLRow row)
			Improved performance
			Improved handling of Unity Objects using internal Resources references

	V.1.0.1 Bug fix releases

	SQL4UnityData.dll April 2020

		Fixed error when converting Color and Color32 blue values

	SQLTableEditor V.1.0.2 April 2020

		Missing column size for data type Color32

	SQLWorkbench V.1.0.3 April 2020

		Fixed bug when asset not in asset root folder
		Increased database object name limit to 20 characters
		Added Column Move Up and Move down to Column Options

	SQLWorkbench V.1.0.2 Februrary 2020

		Added Duplicate Table option

	V.1.0.1 December 2018

		GUI Table Editor added (Tools->SQL for Unity->Table Editor)
		Multiple Database connections allowed
		Minor bug fixes
