using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlantUIManager : MonoBehaviour
{
    public ParquetParser parquetParser;
    public GameObject testimonyUIPrefab;
    public GameObject titleUIPrefab;
    public GameObject testimonyUIWindow;
    public Color selectedLineColor = Color.yellow;
    public TextMeshProUGUI pageNumberDisplay;
    public int linesPerPage = 50;

    private DataFrameRow selectedPlant;
    private Transcript ts;
    private int pageCount = 0;
    private int currentPageIndex = 0;

    public class Transcript
    {
        public List<string> speaker = new List<string>();
        public List<string> dialogue = new List<string>();
        public string fileURL = "";
        public string hearingType = "";
        public string location = "";
        public string date = "";
    }
   
    public async UniTaskVoid SpawnPlantUI(Vector3 coordinates)
    {
        transform.position = coordinates + new Vector3(0, 1, 0);
    }

    public async UniTaskVoid GetTranscript(int plantIndex)
    {
        //selectedPlant = parquetParser.df.Rows[plantIndex];
        //Debug.Log($"df length: {parquetParser.df["file_num"].Length}");
        //Debug.Log($"plantIndex: {plantIndex}");
        int fileNum = Convert.ToInt32(parquetParser.df["file_num"][plantIndex]);
        Debug.Log("filenum: " + fileNum);
        Transcript transcript = new Transcript();

        transcript.fileURL = (string)parquetParser.df["saha_page"][plantIndex];
        transcript.hearingType = (string)parquetParser.df["hearing_type"][plantIndex];
        transcript.location = (string)parquetParser.df["location"][plantIndex];
        transcript.date = (string)parquetParser.df["date"][plantIndex];

        var fileDf = parquetParser.df.Filter(parquetParser.df["file_num"].ElementwiseEquals(fileNum));
        fileDf = fileDf.Filter(fileDf["date"].ElementwiseEquals(transcript.date));
        //Debug.Log("fileDf length: " + fileDf.Rows.Count);
        fileDf = fileDf.OrderBy("file_index");
        for (int i = 0; i < fileDf.Rows.Count; i++)
        {
            transcript.speaker.Add((string)parquetParser.df["speaker"][i]);
            transcript.dialogue.Add((string)parquetParser.df["dialogue"][i]);
        }
        ts = transcript;

        pageCount = (int)Math.Ceiling((double)((ts.speaker.Count + 1) / linesPerPage));
        //Debug.Log(ts);
    }

    public void NextPage()
    {
        if (currentPageIndex < pageCount - 1)
        {
            currentPageIndex++;
            DisplayTranscriptPage(currentPageIndex);
            pageNumberDisplay.SetText($"{currentPageIndex}");
        }
    }
    public void PreviousPage()
    {
        if (currentPageIndex >= 1)
        {
            currentPageIndex--;
            DisplayTranscriptPage(currentPageIndex);
            pageNumberDisplay.SetText($"{currentPageIndex}");
        }
    }

    public async UniTaskVoid DisplayTranscriptPage(int page)
    {
        foreach (Transform child in testimonyUIWindow.transform)
            Destroy(child.gameObject);

        if (currentPageIndex == 0)
        {
            var titleUI = Instantiate(titleUIPrefab, testimonyUIWindow.transform);
            titleUI.GetComponent<TextMeshProUGUI>().SetText($"<u>TRUTH AND RECONCILIATION COMMISSION\n{ts.hearingType}</u>");
            var dateUI = Instantiate(testimonyUIPrefab, testimonyUIWindow.transform);
            dateUI.GetComponent<TextMeshProUGUI>().SetText("<u>DATE:</u><space=1.5em>" + ts.date);
            var locationUI = Instantiate(testimonyUIPrefab, testimonyUIWindow.transform);
            locationUI.GetComponent<TextMeshProUGUI>().SetText("<u>PLACE:</u><space=1.5em>" + ts.location);
        }

        string testimonyContents = "";
        for (int pageIndex = currentPageIndex * pageCount, j = 0; pageIndex < ts.dialogue.Count & j < 50; pageIndex++, j++)
        {
            //if (pageIndex == Convert.ToInt32(selectedPlant[4])) testimonyContents += "<#FFFF00>";
            testimonyContents += $"<u>{ts.speaker[pageIndex]}:</u><space=1.5em>{ts.dialogue[pageIndex]}";
            //if (pageIndex == Convert.ToInt32(selectedPlant[4])) testimonyContents += "</color>";
            testimonyContents += "\n";
        }
        var testimonyUI = Instantiate(testimonyUIPrefab, testimonyUIWindow.transform);
        testimonyUI.GetComponent<TextMeshProUGUI>().SetText(testimonyContents);
        testimonyUI.GetComponent<TextMeshProUGUI>().ForceMeshUpdate();
    }
}
