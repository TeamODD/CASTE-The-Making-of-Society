using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Caste.CardSwipe.EditorTools
{
    /// <summary>
    /// Game 창 해상도를 모바일 세로(1080x1920)로 맞춘다. 없으면 목록에 추가한다.
    /// 메뉴: CASTE → JSW → Game View 1080x1920 (Portrait)
    /// Unity가 공개 API를 주지 않아 리플렉션을 사용한다 (Unity 버전이 바뀌면 동작하지 않을 수 있음).
    /// </summary>
    public static class GameViewPortraitTool
    {
        private const int Width = 1080, Height = 1920;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [MenuItem("CASTE/JSW/Game View 1080x1920 (Portrait)")]
        public static void SetPortrait()
        {
            try
            {
                Assembly asm = typeof(Editor).Assembly;
                Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
                object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                    .GetProperty("instance").GetValue(null);
                object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
                object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
                Type gType = group.GetType();

                int index = FindSize(group, gType);
                if (index < 0)
                {
                    Type sizeType = asm.GetType("UnityEditor.GameViewSize");
                    Type kind = asm.GetType("UnityEditor.GameViewSizeType");
                    object size = Activator.CreateInstance(sizeType,
                        Enum.Parse(kind, "FixedResolution"), Width, Height, "Mobile Portrait");
                    gType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = (int)gType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }

                Type gvType = asm.GetType("UnityEditor.GameView");
                EditorWindow gameView = EditorWindow.GetWindow(gvType, false, null, false);
                gvType.GetProperty("selectedSizeIndex", Flags).SetValue(gameView, index);
                Debug.Log($"[JSW] Game 창 해상도: {Width}x{Height}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JSW] Game 창 해상도 자동 설정 실패 → Game 창 상단에서 직접 1080x1920을 추가해 주세요.\n{e.Message}");
            }
        }

        private static int FindSize(object group, Type gType)
        {
            int total = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
            MethodInfo get = gType.GetMethod("GetGameViewSize");
            for (int i = 0; i < total; i++)
            {
                object s = get.Invoke(group, new object[] { i });
                Type t = s.GetType();
                if ((int)t.GetProperty("width").GetValue(s) == Width &&
                    (int)t.GetProperty("height").GetValue(s) == Height)
                    return i;
            }
            return -1;
        }
    }
}
