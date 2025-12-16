using UnityEditor;

namespace Puffin
{
    public class EditorDialogExample
    {
        public static void Test()
        {
            EditorDialog.DisplayAlertDialog("标题", "测试", "ok", DialogIconType.Info);
        }
    }
}