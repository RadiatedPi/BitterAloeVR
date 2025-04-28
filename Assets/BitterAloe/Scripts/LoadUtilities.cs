using Cysharp.Threading.Tasks;
using System;
using System.Threading.Tasks;
using UnityEngine;


namespace BitterAloe
{
    public class LoadUtilities
    {
        DateTime startTime;
        float frameBudget = 0.05f;
        float loadPercent = 0f;

        public LoadUtilities()
        {
            SetStartTime();
        }

        public LoadUtilities(float frameBudget)
        {
            SetFrameBudget(frameBudget);
            SetStartTime();
        }

        public void SetStartTime()
        {
            startTime = DateTime.Now;
        }

        public void SetFrameBudget(float frameBudget)
        {
            this.frameBudget = frameBudget;
        }

        public async UniTask YieldForFrameBudget()
        {
            TimeSpan timeElapsed = DateTime.Now - startTime;
            if (timeElapsed.TotalSeconds > frameBudget)
            {
                // reset the start time and wait a frame
                startTime = DateTime.Now;
                await UniTask.Yield();
            }
        }

        public void SetLoadPercent(int total, int completed)
        {
            this.loadPercent = completed / total;
        }
    }

}


