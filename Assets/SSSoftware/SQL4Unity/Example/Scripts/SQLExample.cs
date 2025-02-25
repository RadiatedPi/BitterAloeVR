/*
 * Copyright SteveSmith.Software 2018. All rights reserverd
 * 
 * This code file may be modified and/or copied
 * 
 * This code is provided for example purposes only
 */
using System.Collections;
using UnityEngine;

/* A simple example program to show the loading of data from a database and data persistence.
 * 
 * Firstly instantiate GameObjects using Prefebs defined in a Database
 * and place them in either their last known position or a default position.
 * 
 * The objects will then be moved to a fixed end position with their position at the end of each frame saved to the database
 * 
 * This code has been modified to show the use of Stored SQL's and Parameter substitution. The original code has been commented out for clarity.
 * 
*/

public class SQLExample : MonoBehaviour
{

    // Define Main SQL Engine
    SQL4Unity.SQLExecute sql;

    void Start()
    {
        // Initialise engine and Open Database 'MyGameDB'
        sql = new SQL4Unity.SQLExecute("MyGameDB");

        // Initialise result for Players table
        SQL4Unity.SQLResult players = new SQL4Unity.SQLResult();

        // SQL to select all active players
        string query = "Select * from Players where Active=true";

        // Execute the select statement.
        if (!sql.Command(query, players))
        {
            // Output error message is SQL failed
            Debug.Log(players.message);
            return;
        }

        // Loop over each row found
        for (int i = 0; i < players.rowsAffected; i++)
        {
            // Read a row into the Players table helper class
            mygamedb_players player = players.Get<mygamedb_players>(i);

            // Instantiate GameObject from predefined Prefab
            GameObject go = Instantiate((GameObject)player.Prefab);

            // Set GameObject Name from the database
            go.name = player.Title;

            // Check if current position 'CurrPos' is a NULL value (DBNULL.Value)
            if (players.isNull(i, "CurrPos"))
            {
                // FIrst time in so find a default position

                // Initialise result for Position query. Need to use a new result so the Players result is not overwritten.
                SQL4Unity.SQLResult position = new SQL4Unity.SQLResult();

                // SQL to get positions for a Player
                query = "Select * from PlayerPos,Positions where PosID=ID AND PlayerID=" + player.ID.ToString();

                // Execute the select statement
                if (!sql.Command(query, position))
                {
                    // Output error message if SQL failed
                    Debug.Log(position.message);
                    return;
                }

                // Were any rows found?
                if (position.rowsAffected > 0)
                {
                    // Read the first position found
                    Vector3 pos = (Vector3)position.Get(0, "Position");

                    // Move GameObject to position
                    go.transform.position = pos;

                    // Update the database with the found position
                    // DataType.ToString formats the Vector3 into SQL4Unity text format.
                    query = "Update players set currpos =" + SQL4Unity.DataType.ToString(pos) + " where ID=" + player.ID.ToString();

                    // Execute the Update statement
                    if (!sql.Command(query, position))
                    {
                        // Output errormessage if SQL failed
                        Debug.Log(position.message);
                        return;
                    }
                }
            }
            else
            {
                // Restore the position from the last known position 
                go.transform.position = player.CurrPos;
            }
            // Player done 
            Debug.Log("Activated Player " + player.Title + " at position " + go.transform.position);

            // Start the coroutine to move the gameobject to its end position over a 15 second timespan
            StartCoroutine(MoveToEnd(go, new Vector3(6.0f, 5f, 6f), 15f, player.ID));
        }
    }

    public IEnumerator MoveToEnd(GameObject go, Vector3 end, float seconds, int id)
    {
        // Define a result set for the database update
        SQL4Unity.SQLResult playUpd = new SQL4Unity.SQLResult();

        float elapsedTime = 0;
        Vector3 startingPos = go.transform.position;

		// Set up database update template
		//string query = "Update Players set currpos={0} where ID=" + id;

		// Use SQL stored in database
		string command = "Execute UpdPos";
		// This is the SQL for UpdPos
		// Update Players set currpos=%pos% where ID=%id%

		// Set up SQLParameter for parameter substitution
		SQL4Unity.SQLParameter parameters = new SQL4Unity.SQLParameter();
		// Add Parameter value for ID
		parameters.SetValue("id", id);

        while (elapsedTime < seconds)
        {
            go.transform.position = Vector3.Lerp(startingPos, end, (elapsedTime / seconds));
            elapsedTime += Time.deltaTime;

			// Use template to generate the update statement with the current gameobject position
			//string command = string.Format(query, SQL4Unity.DataType.ToString(go.transform.position));

			// Add parameter value for Position
			parameters.SetValue("pos", go.transform.position);

			// Execute the SQL statement without parameters
			//if (!sql.Command(command, playUpd))

			// Execute the SQL statement with parameters
			if (!sql.Command(command, playUpd, parameters))
			{
					// Output errormessage if SQL failed
					Debug.Log(playUpd.message);
            }
            yield return new WaitForEndOfFrame();
        }
        go.transform.position = end;
    }
}
