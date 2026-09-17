using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    public sealed class VoxelMissionBreakdown
    {
        public enum Group { ProgressGained, ProgressLost, IntegrityLost, MultiplierGained, MultiplierLost, CrateRewards }
        public sealed class Entry { public string source; public float amount; public int count; }
        private readonly Dictionary<Group, List<Entry>> entries = new();
        public IReadOnlyList<Entry> Entries(Group group) => entries.TryGetValue(group, out var rows) ? rows : System.Array.Empty<Entry>();
        public float Total(Group group) { float total=0; foreach(var row in Entries(group)) total+=row.amount; return total; }
        public void Clear() => entries.Clear();
        public void Add(Group group,string source,float amount,int count=1)
        {
            if(amount<=0) return;
            if(!entries.TryGetValue(group,out var rows)) entries[group]=rows=new List<Entry>();
            var row=rows.Find(e=>e.source==source);
            if(row==null) { row=new Entry {source=source}; rows.Add(row); }
            row.amount+=amount; row.count+=count;
        }
    }
}
