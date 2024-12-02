using Cysharp.Threading.Tasks;
using Microsoft.Data.Analysis;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PlantUIManager : MonoBehaviour
{
    public ParquetParser parquetParser;

    private DataFrameRow selectedPlant;

    public class Transcript
    {
        public List<string> dialogue;
        public List<string> speaker;
        public string fileURL;
        public string hearingType;
        public string location;
        public string date;
    }

    public async UniTaskVoid SpawnPlantUI(Vector3 coordinates)
    {
        transform.position = coordinates + new Vector3(0,1,0);
    }


    public async UniTask<Transcript> GetTranscript(int plantIndex)
    {
        var fileNum = parquetParser.df["file_num"][plantIndex];

        Transcript transcript = new Transcript();

        transcript.fileURL = (string)parquetParser.df["saha_page"][plantIndex];
        transcript.hearingType = (string)parquetParser.df["hearing_type"][plantIndex];
        transcript.location = (string)parquetParser.df["location"][plantIndex];
        transcript.date = (string)parquetParser.df["date"][plantIndex];

        await Task.Run(() =>
        {
            var fileDf = parquetParser.df[parquetParser.df["file_num"].ElementwiseEquals(fileNum)];
            fileDf = fileDf.OrderBy("file_index");
            for (int i = 0; i < fileDf.Rows.Count; i++)
            {
                transcript.speaker[i] = (string)parquetParser.df["speaker"][i];
                transcript.dialogue[i] = (string)parquetParser.df["dialogue"][i];
            }
        });

        return transcript;
    }
}
