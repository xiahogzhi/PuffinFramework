// using System;
// using Sirenix.OdinInspector;
// using Unity.Cinemachine;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Puffin.Runtime.Core
// {
//     public class LauncherSetting : MonoBehaviour
//     {
//         private static LauncherSetting _instance;
//
//         public static LauncherSetting instance
//         {
//             get
//             {
//                 if (_instance == null)
//                     _instance = FindAnyObjectByType<LauncherSetting>();
//
//                 return _instance;
//             }
//         }
//
//         [Serializable]
//         public class SystemConfig // : IBoxInlineGUI
//         {
//             [LabelText("UI摄像机")] [SerializeField] private Camera _uiCamera;
//             [LabelText("游戏摄像机")] [SerializeField] private Camera _gameCamera;
//             [LabelText("游戏摄像机")] [SerializeField] private CinemachineVirtualCameraBase _mainCamera;
//             [LabelText("UIRoot")] [SerializeField] private Transform _uiRoot;
//             [SerializeField] private Canvas _canvas;
//             [LabelText("输入")] [SerializeField] private PlayerInput _input;
//
//             #region 属性
//
//             public Camera uiCamera => _uiCamera;
//
//             public Camera gameCamera => _gameCamera;
//
//             public Transform uiRoot => _uiRoot;
//
//
//             public Canvas canvas => _canvas;
//
//             public CinemachineVirtualCameraBase mainCamera => _mainCamera;
//
//             public PlayerInput input => _input;
//
//             #endregion
//         }
//
//
//  
//
//         [LabelText("系统配置")] [SerializeField] private SystemConfig _systemConfig;
//       
//
//         public SystemConfig systemConfig => _systemConfig;
//     }
// }