#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    internal static class EmergencyRoadAutoTestInstaller
    {
        private const string GameScenePath="Assets/Scenes/Game.unity";

        static EmergencyRoadAutoTestInstaller()=>EditorApplication.delayCall+=Install;

        private static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||!System.IO.File.Exists(GameScenePath))return;
            var active=SceneManager.GetActiveScene();bool opened=active.path!=GameScenePath;
            var scene=opened?EditorSceneManager.OpenScene(GameScenePath,OpenSceneMode.Additive):active;
            EmergencyRoadAutoTester existing=null;
            foreach(var root in scene.GetRootGameObjects()){existing=root.GetComponentInChildren<EmergencyRoadAutoTester>(true);if(existing!=null)break;}
            if(existing==null){var go=new GameObject("AUTO TEST DRIVER",typeof(EmergencyRoadAutoTester));SceneManager.MoveGameObjectToScene(go,scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("[Emergency Road] Added scene-authored AUTO TEST DRIVER to Game.unity (disabled until Run On Play is checked).");}
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
    }
}
#endif
