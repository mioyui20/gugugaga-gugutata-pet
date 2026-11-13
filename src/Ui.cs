using System.Collections.Generic;

namespace PenguinPet {
    static class Ui {
        public static bool English;
        public static string PetName { get { return English ? "guga" : "咕咕嘎嘎"; } }
        static readonly Dictionary<string,string> EnglishText=new Dictionary<string,string> {
            {"设置","Settings"},{"设置与使用说明","Settings & help"},{"摸摸头","Pet guga"},
            {"让咕咕嘎嘎打个盹","Let guga nap"},{"暂停响应","Pause reactions"},{"继续响应","Resume reactions"},
            {"始终置顶","Always on top"},{"鼠标穿透（从托盘恢复）","Click-through (restore from tray)"},
            {"隐藏宠物","Hide pet"},{"显示宠物","Show pet"},{"回到屏幕右下角","Move to bottom-right"},{"退出","Quit"},
            {"已暂停响应","Reactions paused"},{"监听不可用 · 错误 ","Input unavailable · error "},
            {"已隐藏 · 仍在托盘中","Hidden · available in the tray"},{"陪伴中 · 等待你的操作","Ready to keep you company"},
            {"本次启动：{0} 次敲击 / {1} 次点击","This session: {0} key taps / {1} clicks"},
            {"每一次敲击，都有小小的回应。","A little response to every keystroke."},
            {"宠物大小","Pet size"},{"不透明度","Opacity"},{"显示鼠标轨迹和睡眠气泡","Mouse trail and sleep bubbles"},
            {"拖动移动位置 · 单击摸摸头 · 右键更多设置","Drag: move · Click: pet · Right-click: menu"},
            {"打字敲键盘 · 移动鼠标同步握鼠手\n按住保持按下，松开自动抬手。\n鼠标穿透后，可从系统托盘菜单恢复。","Type to tap. Move the pointer to aim.\nHold a key to press; release it to lift.\nRestore click-through from the tray."},
            {"关闭鼠标穿透","Interact"},{"开启鼠标穿透","Pass through"},
            {"已开启鼠标穿透","Click-through enabled"},
            {"右键系统托盘中的宠物图标，可关闭穿透。","Right-click the pet's tray icon to disable click-through."},
            {"设置未能保存，下次启动会使用默认设置。","Settings could not be saved. Defaults will be used next time."},
            {"键鼠监听线程未能启动。","The input monitoring thread could not start."},
            {"启动失败","Startup failed"},{"全局键鼠监听未能注册，错误：","Could not register global input monitoring. Error: "},
            {"。宠物仍可拖动与互动。",". You can still drag and pet the character."},
            {"右键设置 / 双击摸摸头","Right-click: settings / Double-click: pet"},
            {"来啦","is here"},{"试试打字和移动鼠标吧！右键可打开设置。","Try typing and moving your mouse! Right-click for settings."},
            {"已暂停","Paused"}
        };
        public static string Text(string source) {
            if(source=="{pet}") return PetName;
            if(source=="{pet} / gugugaga pet") return PetName+" / gugugaga pet";
            string result; return English&&EnglishText.TryGetValue(source,out result)?result:source;
        }
    }
}
