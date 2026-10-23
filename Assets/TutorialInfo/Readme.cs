using System;
using UnityEngine;
using UnityEngine.UIElements;

// 编辑器说明页资料卡，不参与游戏战斗。
public class Readme : ScriptableObject
{
    // 页面样式、图标、标题与段落交给编辑器排版。
    public StyleSheet commonStyle;
    public StyleSheet darkStyle;
    public StyleSheet lightStyle;
    public Texture2D icon;
    public string title;
    public Section[] sections;
    // 记录是否已加载过教程窗口布局，防止每次查看都重排编辑器窗口。
    public bool loadedLayout;

    // 段落可序列化保存，包括标题、正文和链接。
    [Serializable]
    public class Section
    {
        public string heading, text, linkText, url;
    }
}
