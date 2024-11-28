using Microsoft.Data.Analysis;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PlantUIManager : MonoBehaviour
{
    public ParquetParser parquetParser;

    private DataFrame df;
    private int selectedPlantIndex;

    public class Transcript
    {
        public List<string> dialogue;
        public List<string> speaker;
        public string fileURL;
        public string hearingType;
        public string location;
        public string date;
    }

    private void Start()
    {
        df = parquetParser.df;
    }

    public async Task<Transcript> GetTranscript(int plantIndex)
    {
        var fileNum = df["file_num"][plantIndex];

        Transcript transcript = new Transcript();

        transcript.fileURL = (string)df["saha_page"][plantIndex];
        transcript.hearingType = (string)df["hearing_type"][plantIndex];
        transcript.location = (string)df["location"][plantIndex];
        transcript.date = (string)df["date"][plantIndex];

        await Task.Run(() =>
        {
            var fileDf = df[df["file_num"].ElementwiseEquals(fileNum)];
            fileDf = fileDf.OrderBy("file_index");
            for (int i = 0; i < fileDf.Rows.Count; i++)
            {
                transcript.speaker[i] = (string)df["speaker"][i];
                transcript.dialogue[i] = (string)df["dialogue"][i];
            }
        });

        return transcript;
    }
}
