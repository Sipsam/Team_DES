using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    public static class RegressionChecks
    {
        // Runs against the newly created in-memory scene AFTER it is saved; no test objects saved.
        public static void Run()
        {
            var root=LevelGenerator.FindRoot();
            var sections=root.GetComponentsInChildren<LevelSection>();
            var mountain=sections.Single(s=>s.section==SectionId.Mountain);
            var river=sections.Single(s=>s.section==SectionId.River);
            var old=mountain.transform.Find("Generated");var untouched=river.transform.Find("Generated");
            var manual=new GameObject("ManualPreservationProbe");manual.transform.SetParent(mountain.transform.Find("Manual"));
            LevelGenerator.Generate(mountain.config,SectionId.Mountain);
            if(manual==null || river.transform.Find("Generated")!=untouched || mountain.transform.Find("Generated")==old) throw new Exception("Section isolation failed");
            Undo.PerformUndo();
            if(old==null || mountain.transform.Find("Generated")!=old || manual==null || river.transform.Find("Generated")!=untouched) throw new Exception("Undo restoration failed");
            UnityEngine.Object.DestroyImmediate(manual);
            System.IO.Directory.CreateDirectory("Docs/Development/Level/V1");
            System.IO.File.AppendAllText("Docs/Development/Level/V1/Validation.txt","\nPASS: section regeneration preserved Manual probe and other section; Undo restored previous generated root.\n");
            Debug.Log("BLOCKOUT REGRESSION PASS");
        }
    }
}
