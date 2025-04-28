using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;


public class Testimony
{
    // dataset properties
    public string speaker { get; set; }
    public string dialogue { get; set; }
    public string file { get; set; }
    public int? file_index { get; set; }
    public string saha_page { get; set; }
    public int? saha_loc { get; set; }
    public string hearing_type { get; set; }
    public string location { get; set; }
    public int? file_num { get; set; }
    public string date { get; set; }
    public float? umap_x { get; set; }
    public float? umap_y { get; set; }
    public int? hdbscan_label { get; set; }
    // custom properties
    public int? index { get; set; }
    public Vector3 levelPosition { get; set; }
}



public class Testimonies
{
    #region Property classes
    public class Speaker
    {
        public int index;
        public string name;
        public Speaker(int index, string speaker)
        {
            this.index = index;
            this.name = speaker;
        }
    }
    public class File
    {
        public int index;
        public string file_path;
        public int file_num;
        public int file_index;
        public File(int index, string file_path, int file_num, int file_index)
        {
            this.index = index;
            this.file_path = file_path;
            this.file_num = file_num;
            this.file_index = file_index;
        }
    }
    public class Saha
    {
        public int index;
        public string page;
        public int loc;
        public Saha(int index, string saha_page, int saha_loc)
        {
            this.index = index;
            this.page = saha_page;
            this.loc = saha_loc;
        }
    }
    public class Hearing_type
    {
        public int index;
        public string type;
        public Hearing_type(int index, string hearing_type)
        {
            this.index = index;
            this.type = hearing_type;
        }
    }
    public class Location
    {
        public int index;
        public string loc;
        public Location(int index, string location)
        {
            this.index = index;
            this.loc = location;
        }
    }
    public class Date
    {
        public int index;
        public string date;
        public Date(int index, string date)
        {
            this.index = index;
            this.date = date;
        }
    }
    public class Umap_x
    {
        public int index;
        public float value;
        public Umap_x(int index, float umap_x)
        {
            this.index = index;
            this.value = umap_x;
        }
    }
    public class Umap_y
    {
        public int index;
        public float value;
        public Umap_y(int index, float umap_y)
        {
            this.index = index;
            this.value = umap_y;
        }
    }
    public class Hdbscan_label
    {
        public int index;
        public int label;
        public Hdbscan_label(int index, int hdbscan_label)
        {
            this.index = index;
            this.label = hdbscan_label;
        }
    }
    #endregion

    public Testimony[] testimonyArray;

    public Speaker[] speakerArray;
    public File[] fileArray;
    public Saha[] sahaArray;
    public Hearing_type[] hearing_typeArray;
    public Location[] locationArray;
    public Date[] dateArray;
    public Umap_x[] umap_xArray;
    public Umap_y[] umap_yArray;
    public Hdbscan_label[] hdbscan_labelArray;

    public Testimonies(List<Testimony> testimonyList)
    {
        testimonyArray = testimonyList.ToArray();
        speakerArray = new Speaker[testimonyList.Count];
        fileArray = new File[testimonyList.Count];
        sahaArray = new Saha[testimonyList.Count];
        hearing_typeArray = new Hearing_type[testimonyList.Count];
        locationArray = new Location[testimonyList.Count];
        dateArray = new Date[testimonyList.Count];
        umap_xArray = new Umap_x[testimonyList.Count];
        umap_yArray = new Umap_y[testimonyList.Count];
        hdbscan_labelArray = new Hdbscan_label[testimonyList.Count];


        for (int i = 0; i < testimonyArray.Length; i++)
        {
            int index = (int)testimonyArray[i].index;
            speakerArray[i] = new Speaker(index, testimonyArray[i].speaker);
            fileArray[i] = new File(index, testimonyArray[i].file, (int)testimonyArray[i].file_num, (int)testimonyArray[i].file_index);
            sahaArray[i] = new Saha(index, testimonyArray[i].saha_page, (int)testimonyArray[i].saha_loc);
            hearing_typeArray[i] = new Hearing_type(index, testimonyArray[i].hearing_type);
            locationArray[i] = new Location(index, testimonyArray[i].location);
            dateArray[i] = new Date(index, testimonyArray[i].date);
            umap_xArray[i] = new Umap_x(index, (float)testimonyArray[i].umap_x);
            umap_yArray[i] = new Umap_y(index, (float)testimonyArray[i].umap_y);
            hdbscan_labelArray[i] = new Hdbscan_label(index, (int)testimonyArray[i].hdbscan_label);
        }
    }

    public async UniTask SortTestimonies()
    {
        List<UniTask> taskList = new List<UniTask>();
        taskList.Add(SortSpeaker());
        taskList.Add(SortFile());
        taskList.Add(SortSaha());
        taskList.Add(SortHearingType());
        taskList.Add(SortDate());
        taskList.Add(SortUmapX());
        taskList.Add(SortUmapY());
        taskList.Add(SortHDBScanLabel());

        await UniTask.WhenAll(taskList);
    }

    #region Property sort methods
    public async UniTask SortSpeaker()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //speakerArray = speakerArray.OrderBy(row => row.name).ThenBy(row => row.index).ToArray();
            Array.Sort(speakerArray, Comparer<Speaker>.Create((x, y) =>
            {
                if (x.name != y.name)
                {
                    return x.name.CompareTo(y.name);
                }
                return x.index.CompareTo(y.index);
            }));
        });
    }
    public async UniTask SortFile()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //fileArray = fileArray.OrderBy(row => row.file_num).ThenBy(row => row.file_index).ThenBy(row => row.index).ToArray();
            Array.Sort(fileArray, Comparer<File>.Create((x, y) =>
            {
                if (x.file_num != y.file_num)
                {
                    return x.file_num.CompareTo(y.file_num);
                }
                else if (x.file_index != y.file_index)
                {
                    return x.file_index.CompareTo(y.file_index);
                }
                return x.index.CompareTo(y.index);
            }));
        });
    }
    public async UniTask SortSaha()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //sahaArray = sahaArray.OrderBy(row => row.page).ThenBy(row => row.loc).ThenBy(row => row.index).ToArray();
            Array.Sort(sahaArray, Comparer<Saha>.Create((x, y) =>
            {
                if (x.page != y.page)
                {
                    return x.page.CompareTo(y.page);
                }
                else if (x.loc != y.loc)
                {
                    return x.loc.CompareTo(y.loc);
                }
                return x.index.CompareTo(y.index);
            }));
        });
    }
    public async UniTask SortHearingType()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //hearing_typeArray = hearing_typeArray.OrderBy(row => row.type).ThenBy(row => row.index).ToArray();
            Array.Sort(hearing_typeArray, Comparer<Hearing_type>.Create((x, y) =>
            {
                if (x.type != y.type)
                {
                    return x.type.CompareTo(y.type);
                }
                return x.index.CompareTo(y.index);
            }));
        });
    }
    public async UniTask SortDate()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //dateArray = dateArray.OrderBy(row => row.date).ThenBy(row => row.index).ToArray();
            Array.Sort(dateArray, Comparer<Date>.Create((x, y) =>
            {
                if (x.date != y.date)
                {
                    return x.date.CompareTo(y.date);
                }
                return x.index.CompareTo(y.index);
            }));
        });
    }
    public async UniTask SortUmapX()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //umap_xArray = umap_xArray.OrderBy(row => row.value).ToArray();
            Array.Sort(umap_xArray, Comparer<Umap_x>.Create((x, y) => x.value.CompareTo(y.value)));
        });
    }
    public async UniTask SortUmapY()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //umap_yArray = umap_yArray.OrderBy(row => row.value).ToArray();
            Array.Sort(umap_yArray, Comparer<Umap_y>.Create((x, y) => x.value.CompareTo(y.value)));
        });
    }
    public async UniTask SortHDBScanLabel()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            //hdbscan_labelArray = hdbscan_labelArray.OrderBy(row => row.label).ThenBy(row => row.index).ToArray();
            Array.Sort(hdbscan_labelArray, Comparer<Hdbscan_label>.Create((x, y) =>
            {
                if (x.label != y.label)
                {
                    return x.label.CompareTo(y.label);
                }
                return x.index.CompareTo(y.index);
            }));
        });
    }
    #endregion
}
