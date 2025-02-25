/*
 * This File and its contents are Copyright SteveSmith.Software 2018. All rights reserverd
 *
 * This code file may be modified and/or copied
 *
 * This code is provided for example purposes only
 */
using UnityEngine;

/* 
 * Reset Database to its initial state
*/

public class InitDB : MonoBehaviour
{
    // Define Main SQL Engine
    SQL4Unity.SQLExecute sql;
    int posID = 0;

    void Start()
    {
        // Open the database MyGameDB
        sql = new SQL4Unity.SQLExecute("MyGameDB");

        // Initialise a result set
        SQL4Unity.SQLResult result = new SQL4Unity.SQLResult();

        // Cleanup Players table
        string query = "Delete from Players";
        // Could also use "Truncate Players" instead of "Delete from Players". See the documentation for more details.
        Debug.Log(query);
        if (!sql.Command(query, result))
        {
            Debug.Log(result.message);
            return;
        }
        Debug.Log("Players delete : "+result.message);

        // Cleanup Positions table
        query = "Delete from Positions";
        Debug.Log(query);
        if (!sql.Command(query, result))
        {
            Debug.Log(result.message);
            return;
        }
        Debug.Log("Positions delete : "+result.message);

        // Cleanup Player Position table
        query = "Delete from PlayerPos";
        Debug.Log(query);
        if (!sql.Command(query, result))
        {
            Debug.Log(result.message);
            return;
        }
        Debug.Log("PlayerPos delete : "+result.message);

        // Initialise the four Players and their initial positions
        if (!PlayerSetup("Cube", 10, new Vector3(1, 1, 1), result)) return;
        if (!PlayerSetup("Sphere", 20, new Vector3(2, 2, 2), result)) return;
        if (!PlayerSetup("Capsule", 30, new Vector3(3, 1.5f, 3), result)) return;
        if (!PlayerSetup("Cylinder", 40, new Vector3(4, 0.5f, 4), result)) return;
        sql.Close(false);
        Debug.Log("Database Initialisation Complete");
    }

    bool PlayerSetup(string title, int id, Vector3 pos, SQL4Unity.SQLResult result)
    {
        // Insert a Player row into the database

        // SQL4Unity.DataType.ToString(title) will return the string formatted for SQL statement usage. i,e, in single quotes

        // {NAME:"+title+"} is the format for the Prefab column which is defined as a 'Resource' datatype. In this case a GameObject prefab.

        string query = string.Format("Insert into Players (ID,Title,Prefab) values ({0},{1},{2})",id, SQL4Unity.DataType.ToString(title),"{NAME:"+title+"}");
        Debug.Log(query);
        if (!sql.Command(query, result))
        {
            Debug.Log("Insert Players "+result.message);
            return false;
        }

        // Insert a Position row into the database
        query = string.Format("Insert into Positions (ID, Position) values ({0}, {1})", posID, SQL4Unity.DataType.ToString(pos));
        Debug.Log(query);
        if (!sql.Command(query, result))
        {
            Debug.Log("Insert Positions "+result.message);
            return false;
        }

        // Insert a default player position into the database
        query = string.Format("Insert into PlayerPos (PlayerID, PosID) values ({0},{1})", id, posID);
        Debug.Log(query);
        if (!sql.Command(query, result))
        {
            Debug.Log("Insert PlayerPos "+result.message);
            return false;
        }

        Debug.Log("Player "+ title + " with ID " + id + " defined. Default position will be " + pos.ToString());
        posID++;
        return true;
    }
}
