using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace GreatClock.Common.SerializeTools {

	public class SerializedComponentWindow : EditorWindow, IHasCustomMenu {

		#region MenuItem

		[MenuItem("GreatClock/Serialize Tools/C# Serialized Component")]
		[MenuItem("Assets/GreatClock/Serialize Tools/C# Serialized Component", false)]
		static void OpenSerializedComponentWindow() {
			SerializedComponentWindow win = GetWindow<SerializedComponentWindow>("Component Viewer");
			win.minSize = new Vector2(480f, 300f);
			win.mObj = Selection.activeGameObject;
			win.Show();
		}


		[MenuItem("Assets/GreatClock/Serialize Tools/C# Serialized Component", true)]
		static bool CanAssetOpenSerializedComponentWindow() {
			return Selection.activeGameObject != null;
		}

		public void AddItemsToMenu(GenericMenu menu) {
			menu.AddItem(s_content_language_en, s_current_language == eLanguage.EN, ChangeLanguage, eLanguage.EN);
			menu.AddItem(s_content_language_chs, s_current_language == eLanguage.CHS, ChangeLanguage, eLanguage.CHS);
		}

		#endregion

		#region supported component types

		[SupportedComponentType]
		static SupportedTypeData DefineTypeTransform() {
			return new SupportedTypeData(typeof(Transform), int.MinValue);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeRectTransform() {
			return new SupportedTypeData(typeof(RectTransform), int.MinValue);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeText() {
			return new SupportedTypeData(typeof(UnityEngine.UI.Text), 100).SetRequireClearOnRecycle(false);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeButton() {
			return new SupportedTypeData(typeof(UnityEngine.UI.Button), 101);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeToggle() {
			return new SupportedTypeData(typeof(UnityEngine.UI.Toggle), 101);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeSlider() {
			return new SupportedTypeData(typeof(UnityEngine.UI.Slider), 101);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeScrollbar() {
			return new SupportedTypeData(typeof(UnityEngine.UI.Scrollbar), 101);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeInputField() {
			return new SupportedTypeData(typeof(UnityEngine.UI.InputField), 101);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeImage() {
			return new SupportedTypeData(typeof(UnityEngine.UI.Image), 102).SetRequireClearOnRecycle(false);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeRawImage() {
			return new SupportedTypeData(typeof(UnityEngine.UI.RawImage), 102).SetRequireClearOnRecycle(false);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeCanvas() {
			return new SupportedTypeData(typeof(Canvas), 110);
		}
		[SupportedComponentType]
		static SupportedTypeData DefineTypeCanvasGroup() {
			return new SupportedTypeData(typeof(CanvasGroup), 110);
		}

		private static Dictionary<int, SupportedTypeData> supported_type_datas;

		private static SupportedTypeData GetSupportedTypeData(Type type) {
			if (type == null) { return null; }
			if (supported_type_datas == null) { supported_type_datas = new Dictionary<int, SupportedTypeData>(); }
			if (supported_type_datas.Count <= 0) {
				Type tComponent = typeof(Component);
				Type attr = typeof(SupportedComponentTypeAttribute);
				Type dele = typeof(DefineSupportedTypeDelegate);
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				for (int i = 0, imax = assemblies.Length; i < imax; i++) {
					Type[] types = assemblies[i].GetTypes();
					for (int j = 0, jmax = types.Length; j < jmax; j++) {
						Type tt = types[j];
						MethodInfo[] methods = tt.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
						for (int k = 0, kmax = methods.Length; k < kmax; k++) {
							MethodInfo method = methods[k];
							if (!Attribute.IsDefined(method, attr)) { continue; }
							DefineSupportedTypeDelegate func = Delegate.CreateDelegate(dele, method, false) as DefineSupportedTypeDelegate;
							if (func == null) {
								Debug.LogErrorFormat("Method '{0}.{1}' with 'SupportedComponentType' should match 'DefineSupportedTypeDelegate' !", tt.FullName, method.Name);
								continue;
							}
							SupportedTypeData td = null;
							try { td = func(); } catch (Exception e) { Debug.LogException(e); }
							if (td == null || td.type == null) { continue; }
							if (!td.type.IsSubclassOf(tComponent)) {
								Debug.LogErrorFormat("Type should be sub class of 'Component' ! Error type : '{0}' .", td.type.FullName);
								continue;
							}
							string showName = td.showName;
							if (string.IsNullOrEmpty(showName)) { showName = td.type.Name; }
							string codeTypeName = td.codeTypeName;
							string nameSpace = td.nameSpace;
							if (string.IsNullOrEmpty(codeTypeName)) {
								codeTypeName = td.type.Name;
								nameSpace = td.type.Namespace;
							}
							string variableName = td.variableName;
							if (string.IsNullOrEmpty(variableName)) {
								variableName = td.type.Name;
								variableName = variableName.Substring(0, 1).ToLower() + variableName.Substring(1);
							}
							td = new SupportedTypeData(td.type, td.priority, showName, nameSpace, codeTypeName, variableName, td.requireClearOnRecycle, td.abortChild);
							supported_type_datas.Add(td.type.GetHashCode(), td);
						}
					}
				}
			}
			SupportedTypeData data;
			return supported_type_datas.TryGetValue(type.GetHashCode(), out data) ? data : null;
		}

		private static int SortSupportedTypeDatas(SupportedTypeData l, SupportedTypeData r) {
			if (l.priority == r.priority) { return string.Compare(l.type.Name, r.type.Name); }
			return l.priority < r.priority ? -1 : 1;
		}

		#endregion

		#region language

		private enum eLanguage { EN, CHS }

		private static bool s_language_inited = false;
		private static eLanguage s_current_language;

		private static GUIContent s_content_language_chs;
		private static GUIContent s_content_language_en;
		private static GUIContent s_content_code_path;
		private static GUIContent s_content_manual;
		private static GUIContent s_content_select;
		private static GUIContent s_content_browse;
		private static GUIContent s_content_delete;
		private static GUIContent s_content_namespace;
		private static GUIContent s_content_default_partial;
		private static GUIContent s_content_default_public_property;
		private static GUIContent s_content_default_item_partial;
		private static GUIContent s_content_default_item_public_property;
		private static GUIContent s_content_node_list;
		private static GUIContent s_content_partial;
		private static GUIContent s_content_public_property;
		private static GUIContent s_content_base_class;
		private static GUIContent s_content_gen_and_mount;
		private static GUIContent s_content_preview_code;
		private static string s_string_select_code_folder;
		private static GUILayoutOption s_layout_width_2;
		private static GUILayoutOption s_layout_width_5;

		private void ChangeLanguage(object para) {
			SetLanguage((eLanguage)para);
		}

		private static void SetLanguage(eLanguage lang) {
			if (s_language_inited && s_current_language == lang) { return; }
			s_language_inited = true;
			if (s_current_language != lang) {
				s_current_language = lang;
				EditorPrefs.SetInt(GetKey("lang"), (int)lang);
			}
			switch (lang) {
				case eLanguage.CHS:
					s_content_language_chs = new GUIContent("语言/简体中文");
					s_content_language_en = new GUIContent("语言/English");
					s_content_code_path = new GUIContent("生成代码目录");
					s_content_manual = new GUIContent("手动");
					s_content_select = new GUIContent("选择");
					s_content_browse = new GUIContent("浏览");
					s_content_delete = new GUIContent("删除");
					s_content_namespace = new GUIContent("代码命名空间");
					s_content_default_partial = new GUIContent("默认使用partial类");
					s_content_default_public_property = new GUIContent("默认使用public属性");
					s_content_default_item_partial = new GUIContent("默认item使用partial类");
					s_content_default_item_public_property = new GUIContent("默认item使用public属性");
					s_content_node_list = new GUIContent("序列化节点组件列表");
					s_content_partial = new GUIContent("使用partial类");
					s_content_public_property = new GUIContent("使用public属性");
					s_content_base_class = new GUIContent("继承自基类");
					s_content_gen_and_mount = new GUIContent("生成并挂载");
					s_content_preview_code = new GUIContent("预览代码");
					s_string_select_code_folder = "选择生成代码存储目录";
					s_layout_width_2 = GUILayout.Width(36f);
					s_layout_width_5 = GUILayout.Width(80f);
					break;
				case eLanguage.EN:
					s_content_language_chs = new GUIContent("Language/简体中文");
					s_content_language_en = new GUIContent("Language/English");
					s_content_code_path = new GUIContent("Generated Code Path");
					s_content_manual = new GUIContent("Manual");
					s_content_select = new GUIContent("Select");
					s_content_browse = new GUIContent("Browse");
					s_content_delete = new GUIContent("Delete");
					s_content_namespace = new GUIContent("Name Space in Code");
					s_content_default_partial = new GUIContent("Default Partial Class");
					s_content_default_public_property = new GUIContent("Default Public Property");
					s_content_default_item_partial = new GUIContent("Default i_ Partial Class");
					s_content_default_item_public_property = new GUIContent("Default i_ Public Property");
					s_content_node_list = new GUIContent("Serialized Node and Property List");
					s_content_partial = new GUIContent("Partial Class");
					s_content_public_property = new GUIContent("Public Property");
					s_content_base_class = new GUIContent("Base Class");
					s_content_gen_and_mount = new GUIContent("Generate & Mount");
					s_content_preview_code = new GUIContent("Code Preview");
					s_string_select_code_folder = "Select Folder to Store Generated Code";
					s_layout_width_2 = GUILayout.Width(52f);
					s_layout_width_5 = GUILayout.Width(120f);
					break;
			}
		}

		[InitializeOnLoadMethod]
		private static void InitLanguage() {
			s_current_language = (eLanguage)EditorPrefs.GetInt(GetKey("lang"));
			SetLanguage(s_current_language);
		}


		#endregion

		#region core api

		private static Regex variable_regex = new Regex(@"^[_a-zA-Z][_0-9a-zA-Z]*$");
		private static bool MatchVariableName(string name) {
			if (string.IsNullOrEmpty(name)) { return false; }
			return variable_regex.IsMatch(name);
		}

		private static ObjectComponents CollectComponents(Transform root, string rootName, string cls, string clsVar) {
			if (root == null) { return null; }
			List<ObjectComponents> components = new List<ObjectComponents>();
			Stack<Transform> trans = new Stack<Transform>(64);
			trans.Push(root);
			while (trans.Count > 0) {
				Transform t = trans.Pop();
				string name = t.name;
				bool abort = name.StartsWith("~");
				if (abort) { name = name.Substring(1); }
				bool isItem = t != root && name.StartsWith("i_");
				while (true) {
					ObjectComponents ocs = null;
					if (t == root) {
						ocs = new ObjectComponents(t.gameObject, "Self", null, null, null);
						components.Add(ocs);
						if (ocs.AbortChild) { abort = true; }
						break;
					}
					if (!isItem && !name.StartsWith("m_")) { break; }
					name = name.Substring(2, name.Length - 2);
					string varName = name;
					string typeName = null;
					string typeVarName = null;
					if (isItem) {
						typeName = string.Concat(cls, "_", name);
						typeVarName = name;
						int ivv = name.IndexOf("||");
						if (ivv >= 0) {
							varName = name.Substring(0, ivv);
							typeName = name.Substring(ivv + 2, name.Length - ivv - 2);
							typeVarName = typeName;
						} else {
							int iv = name.IndexOf('|');
							if (iv >= 0) {
								varName = name.Substring(0, iv);
								typeVarName = name.Substring(iv + 1, name.Length - iv - 1);
								typeName = string.Concat(cls, "_", typeVarName);
							}
						}
						if (!MatchVariableName(typeName)) { break; }
					}
					if (!MatchVariableName(varName)) { break; }
					if (isItem) {
						ocs = CollectComponents(t, varName, typeName, typeVarName);
					}
					if (ocs == null) {
						ocs = new ObjectComponents(t.gameObject, varName, null, null, null);
						if (ocs.AbortChild) { abort = true; }
					}
					components.Add(ocs);
					break;
				}
				if (!isItem && !abort) {
					for (int i = t.childCount - 1; i >= 0; i--) {
						trans.Push(t.GetChild(i));
					}
				}
			}
			return new ObjectComponents(root.gameObject, rootName, cls, clsVar, components);
		}

		#endregion

		#region assist

		private class CodeProperties {
			public string nameSpace;
			public bool partialClass;
			public string className;
			public string baseClass;
			public bool publicProperty;
		}

		static Regex reg_namespace = new Regex(@"namespace\s+((\S+\s*\.\s*)*\S+)\s*\{");
		static Regex reg_class_def = new Regex(@"public\s+(partial\s+){0,1}class\s+(\S+)\s*:\s*(\S+)");
		static Regex reg_subclass_start = new Regex(@"\[System\.Serializable\]");
		static Regex reg_public_property = new Regex(@"public\s+\S+\s+\S+\s*\{\s*get\s*\{\s*return\s+\S+\s*;\s*\}\s*\}");
		private static CodeProperties CheckCodeAtPath(string path) {
			if (!File.Exists(path)) { return null; }
			string code = null;
			try {
				code = File.ReadAllText(path);
			} catch (Exception e) {
				Debug.LogException(e);
			}
			if (code == null) { return null; }
			Match matchClassDefine = reg_class_def.Match(code);
			if (!matchClassDefine.Success) { return null; }
			int i0 = matchClassDefine.Index + matchClassDefine.Length;
			GroupCollection groups = matchClassDefine.Groups;
			CodeProperties cp = new CodeProperties();
			cp.partialClass = groups[1].Success;
			cp.className = groups[2].Value;
			cp.baseClass = groups[3].Value;
			Match matchNS = reg_namespace.Match(code);
			string ns = matchNS.Success ? matchNS.Groups[1].Value : null;
			if (!string.IsNullOrEmpty(ns)) {
				ns = ns.Replace(" ", "").Replace("\t", "");
			}
			cp.nameSpace = ns;
			Match matchSubclassStart = reg_subclass_start.Match(code, i0);
			int i1 = code.Length;
			if (matchSubclassStart.Success) { i1 = matchSubclassStart.Index; }
			cp.publicProperty = reg_public_property.Match(code, i0, i1 - i0).Success;
			return cp;
		}

		#endregion

		private static MD5CryptoServiceProvider md5_calc;
		private static string GetKey(string key) {
			if (md5_calc == null) {
				md5_calc = new MD5CryptoServiceProvider();
			}
			string str = string.Concat(Application.dataPath, "SerializedComponent4CSharp", key);
			byte[] bytes = Encoding.UTF8.GetBytes(str);
			return BitConverter.ToString(md5_calc.ComputeHash(bytes));
		}

		private GameObject mObj;
		private GameObject mPrevObj;
		private ObjectComponents mRootComponents = null;
		private List<ObjectComponentsWithIndent> mDrawingComponents = new List<ObjectComponentsWithIndent>();

		private bool mFolderManualEdit = false;
		private string[] mFolderList;
		private int mFolderIndex;
		private string mFolder;

		private List<string> mUsedNameSpaces = new List<string>();
		private bool mNameSpaceManualEdit = false;
		private string[] mNameSpaceList;
		private int mNameSpaceIndex;

		private List<string> mUsedBaseClasss = new List<string>();
		private string[] mBaseClassList;

		private bool mDefaultPartialClass;
		private bool mDefaultPublicProperty;
		private bool mDefaultPartialItemClass;
		private bool mDefaultPublicItemProperty;

		private bool mGUIStyleInited = false;
		private GUIStyle mStyleBoldLabel;
		private GUIStyle mStyleBox;

		private bool mToSetSerializedObjects = false;

		private Vector2 mScroll;

		private bool mForceUpdate = false;

		void OnEnable() {
			ResetNameSpaceList();
			ResetBaseClassList();
			mFolder = EditorPrefs.GetString(GetKey("prev_folder"), "Assets");
			mFolderIndex = -1;
			mDefaultPartialClass = EditorPrefs.GetBool(GetKey("partial_class"), false);
			mDefaultPublicProperty = EditorPrefs.GetBool(GetKey("public_property"), true);
			mDefaultPartialItemClass = EditorPrefs.GetBool(GetKey("partial_item_class"), false);
			mDefaultPublicItemProperty = EditorPrefs.GetBool(GetKey("public_item_property"), true);
			mPrevObj = null;
			if (mObj != null) {
				string key = GetKey("set_serialized_objects");
				if (EditorPrefs.GetString(key, null) == mObj.name) {
					mToSetSerializedObjects = true;
				}
				EditorPrefs.DeleteKey(key);
			}
		}

		void OnFocus() {
			List<string> folders = new List<string>();
			Queue<string> toCheckFolders = new Queue<string>();
			toCheckFolders.Enqueue("Assets");
			while (toCheckFolders.Count > 0) {
				string folder = toCheckFolders.Dequeue();
				folders.Add(folder);
				string[] subFolders = AssetDatabase.GetSubFolders(folder);
				for (int i = 0, imax = subFolders.Length; i < imax; i++) {
					toCheckFolders.Enqueue(subFolders[i].Replace('\\', '/'));
				}
			}
			mFolderList = folders.ToArray();
			mForceUpdate = true;
		}

		void OnGUI() {
			if (!mGUIStyleInited) {
				mGUIStyleInited = true;
				mStyleBoldLabel = "BoldLabel";
				mStyleBox = GUI.skin.FindStyle("OL Box") ?? GUI.skin.FindStyle("CN Box");
			}
			EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);
			EditorGUILayout.BeginHorizontal();
			mObj = EditorGUILayout.ObjectField("Game Object", mObj, typeof(GameObject), true) as GameObject;
			EditorGUILayout.EndHorizontal();
			if (mObj != mPrevObj || mForceUpdate) {
				mPrevObj = mObj;
				mForceUpdate = false;
				mDrawingComponents.Clear();
				if (mObj != null) {
					mRootComponents = CollectComponents(mObj.transform, mObj.name, mObj.name, null);
					if (mRootComponents != null) {
						string folder = mFolderIndex < 0 ? null : mFolderList[mFolderIndex].Replace('\\', '/');
						if (!string.IsNullOrEmpty(folder) && !folder.EndsWith("/")) { folder = folder + "/"; }
						Stack<ObjectComponentsWithIndent> componentsStack = new Stack<ObjectComponentsWithIndent>();
						List<string> folders = new List<string>();
						string[] scripts = AssetDatabase.FindAssets("t:MonoScript");
						string pathTpl = "/" + mRootComponents.cls + ".cs";
						for (int i = 0, imax = scripts.Length; i < imax; i++) {
							string scriptPath = AssetDatabase.GUIDToAssetPath(scripts[i]);
							if (!scriptPath.EndsWith(pathTpl)) { continue; }
							folders.Add(scriptPath.Substring(0, scriptPath.Length - mRootComponents.cls.Length - 3));
						}
						SortedList<int, KeyValuePair<string, string>> sortedFolders = new SortedList<int, KeyValuePair<string, string>>();
						if (!string.IsNullOrEmpty(folder) && !folders.Contains(folder)) { folders.Add(folder); }
						for (int fi = folders.Count - 1; fi >= 0; fi--) {
							string f = folders[fi];
							string prevNS = null;
							int score = f == folder ? 10 : 0;
							ObjectComponentsWithIndent ocwi = new ObjectComponentsWithIndent();
							ocwi.indent = 0;
							ocwi.components = mRootComponents;
							componentsStack.Push(ocwi);
							while (componentsStack.Count > 0) {
								ocwi = componentsStack.Pop();
								List<ObjectComponents> ocsList = ocwi.components.itemComponents;
								if (ocsList != null) {
									for (int i = ocsList.Count - 1; i >= 0; i--) {
										ObjectComponentsWithIndent nocwi = new ObjectComponentsWithIndent();
										nocwi.indent = ocwi.indent + 1;
										nocwi.components = ocsList[i];
										componentsStack.Push(nocwi);
									}
								}
								CodeProperties cp = null;
								if (!string.IsNullOrEmpty(ocwi.components.cls)) {
									cp = CheckCodeAtPath(string.Concat(f, ocwi.components.cls, ".cs"));
								}
								if (cp != null && cp.className == ocwi.components.cls) {
									score += 15;
									if (ocwi.indent <= 0) {
										prevNS = cp.nameSpace;
										if (!string.IsNullOrEmpty(prevNS) && prevNS == mNameSpaceList[mNameSpaceIndex]) {
											score += 100;
										}
									}
								}
							}
							if (!sortedFolders.ContainsKey(-score)) {
								sortedFolders.Add(-score, new KeyValuePair<string, string>(f, prevNS));
							}
						}
						foreach (KeyValuePair<int, KeyValuePair<string, string>> kv in sortedFolders) {
							string pFolder = kv.Value.Key;
							string prevNS = kv.Value.Value;
							int index = -1;
							for (int i = mFolderList.Length - 1; i >= 0; i--) {
								string fi = mFolderList[i];
								if (fi == pFolder || (fi.Length == pFolder.Length - 1 && pFolder.StartsWith(fi))) {
									index = i;
									break;
								}
							}
							if (index < 0) { continue; }
							mFolderIndex = index;
							mFolder = mFolderList[mFolderIndex];
							if (prevNS == null) { prevNS = ""; }
							mNameSpaceIndex = mUsedNameSpaces.IndexOf(prevNS);
							if (mNameSpaceIndex < 0) {
								mNameSpaceIndex = mNameSpaceList.Length - 1;
								mNameSpaceList[mNameSpaceIndex] = prevNS;
							} else if (mNameSpaceManualEdit) {
								mNameSpaceList[mNameSpaceList.Length - 1] = prevNS;
							} else {
								mNameSpaceList[mNameSpaceList.Length - 1] = "";
							}
							mDrawingComponents.Clear();
							ObjectComponentsWithIndent ocwi = new ObjectComponentsWithIndent();
							ocwi.indent = 0;
							ocwi.components = mRootComponents;
							componentsStack.Push(ocwi);
							while (componentsStack.Count > 0) {
								ocwi = componentsStack.Pop();
								mDrawingComponents.Add(ocwi);
								List<ObjectComponents> ocsList = ocwi.components.itemComponents;
								if (ocsList != null) {
									for (int i = ocsList.Count - 1; i >= 0; i--) {
										ObjectComponentsWithIndent nocwi = new ObjectComponentsWithIndent();
										nocwi.indent = ocwi.indent + 1;
										nocwi.components = ocsList[i];
										componentsStack.Push(nocwi);
									}
								}
								if (ocwi.indent <= 0) {
									ocwi.components.partialClass = mDefaultPartialClass;
									ocwi.components.publicProperty = mDefaultPublicProperty;
								} else {
									ocwi.components.partialClass = mDefaultPartialItemClass;
									ocwi.components.publicProperty = mDefaultPublicItemProperty;
								}
								if (!string.IsNullOrEmpty(ocwi.components.cls)) {
									CodeProperties cp = CheckCodeAtPath(string.Concat(pFolder, ocwi.components.cls, ".cs"));
									if (cp != null && cp.className == ocwi.components.cls) {
										ocwi.components.partialClass = cp.partialClass;
										ocwi.components.publicProperty = cp.publicProperty;
										ocwi.components.baseClass = cp.baseClass;
										ocwi.components.baseClassIndex = -1;
									}
								}
							}
							break;
						}
						if (mFolderIndex < 0) { ResetFolderIndex(); }
					}
				}
			}
			if (mObj == null && mDrawingComponents.Count > 0) {
				mDrawingComponents.Clear();
			}
			GUILayout.Space(8f);
			#region code folder
			EditorGUILayout.LabelField(s_content_code_path);
			EditorGUILayout.BeginHorizontal();
			if (mFolderManualEdit) {
				mFolder = EditorGUILayout.DelayedTextField(mFolder);
			} else {
				mFolderIndex = EditorGUILayout.Popup(mFolderIndex, mFolderList);
				mFolder = mFolderList[mFolderIndex];
			}
			if (GUILayout.Button(mFolderManualEdit ? s_content_select : s_content_manual, s_layout_width_2)) {
				mFolderManualEdit = !mFolderManualEdit;
				if (!mFolderManualEdit) {
					ResetFolderIndex();
				}
			}
			if (GUILayout.Button(s_content_browse, s_layout_width_2)) {
				string folder = EditorUtility.SaveFolderPanel(s_string_select_code_folder, mFolder, "");
				if (!string.IsNullOrEmpty(folder)) {
					if (folder.StartsWith(Application.dataPath)) {
						mFolder = folder.Substring(Application.dataPath.Length - 6);
						if (!mFolderManualEdit) {
							ResetFolderIndex();
						}
					}
				}
			}
			EditorGUILayout.EndHorizontal();
			#endregion
			GUILayout.Space(8f);
			#region name space
			EditorGUILayout.BeginHorizontal();
			if (mNameSpaceManualEdit) {
				int index = mNameSpaceList.Length - 1;
				EditorGUI.BeginChangeCheck();
				mNameSpaceList[index] = EditorGUILayout.DelayedTextField(s_content_namespace, mNameSpaceList[index]);
				if (EditorGUI.EndChangeCheck()) {
					mNameSpaceIndex = index;
				}
			} else {
				mNameSpaceIndex = EditorGUILayout.Popup(s_content_namespace, mNameSpaceIndex, mNameSpaceList);
			}
			if (GUILayout.Button(mNameSpaceManualEdit ? s_content_select : s_content_manual, s_layout_width_2)) {
				mNameSpaceManualEdit = !mNameSpaceManualEdit;
				if (mNameSpaceManualEdit) {
					mNameSpaceList[mNameSpaceList.Length - 1] = mNameSpaceList[mNameSpaceIndex];
				}
			}
			if (GUILayout.Button(s_content_delete, s_layout_width_2)) {
				if (mNameSpaceIndex == mNameSpaceList.Length - 1) {
					mNameSpaceList[mNameSpaceIndex] = "";
				} else {
					mUsedNameSpaces.RemoveAt(mNameSpaceIndex);
					SaveUsedNameSpaces();
					ResetNameSpaceList();
				}
			}
			EditorGUILayout.EndHorizontal();
			#endregion
			EditorGUI.BeginChangeCheck();
			mDefaultPartialClass = EditorGUILayout.Toggle(s_content_default_partial, mDefaultPartialClass);
			if (EditorGUI.EndChangeCheck()) {
				EditorPrefs.SetBool(GetKey("partial_class"), mDefaultPartialClass);
			}
			EditorGUI.BeginChangeCheck();
			mDefaultPublicProperty = EditorGUILayout.Toggle(s_content_default_public_property, mDefaultPublicProperty);
			if (EditorGUI.EndChangeCheck()) {
				EditorPrefs.SetBool(GetKey("public_property"), mDefaultPublicProperty);
			}
			EditorGUI.BeginChangeCheck();
			mDefaultPartialItemClass = EditorGUILayout.Toggle(s_content_default_item_partial, mDefaultPartialItemClass);
			if (EditorGUI.EndChangeCheck()) {
				EditorPrefs.SetBool(GetKey("partial_item_class"), mDefaultPartialItemClass);
			}
			EditorGUI.BeginChangeCheck();
			mDefaultPublicItemProperty = EditorGUILayout.Toggle(s_content_default_item_public_property, mDefaultPublicItemProperty);
			if (EditorGUI.EndChangeCheck()) {
				EditorPrefs.SetBool(GetKey("public_item_property"), mDefaultPublicItemProperty);
			}
			GUILayout.Space(8f);
			EditorGUILayout.LabelField(s_content_node_list);
			mScroll = EditorGUILayout.BeginScrollView(mScroll, false, false);
			int count = mDrawingComponents.Count;
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(4f);
			EditorGUILayout.BeginVertical();
			for (int i = 0; i < count; i++) {
				Color cachedBgColor = GUI.backgroundColor;
				if ((i & 1) == 0) {
					GUI.backgroundColor = cachedBgColor * 0.8f;
				}
				int indent = mDrawingComponents[i].indent;
				if (indent > 0) {
					EditorGUILayout.BeginHorizontal();
					GUILayout.Space(12f * indent);
				}
				EditorGUILayout.BeginVertical(mStyleBox, GUILayout.MinHeight(10f));
				GUI.backgroundColor = cachedBgColor;
				ObjectComponents ocs = mDrawingComponents[i].components;
				int cCount = ocs.Count;
				EditorGUILayout.LabelField(ocs.name, mStyleBoldLabel);
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(12f);
				EditorGUILayout.BeginVertical();
				for (int j = 0; j < cCount; j++) {
					ComponentData cd = ocs[j];
					EditorGUILayout.ObjectField(cd.type.showName, cd.component, cd.type.type, true);
				}
				if (ocs.itemComponents != null) {
					ocs.partialClass = EditorGUILayout.Toggle(s_content_partial, ocs.partialClass);
					ocs.publicProperty = EditorGUILayout.Toggle(s_content_public_property, ocs.publicProperty);
					EditorGUILayout.BeginHorizontal();
					if (ocs.baseClassIndex < 0) {
						ocs.baseClass = EditorGUILayout.DelayedTextField(s_content_base_class, ocs.baseClass);
					} else {
						EditorGUI.BeginChangeCheck();
						mBaseClassList[mBaseClassList.Length - 1] = ocs.baseClassIndex >= mBaseClassList.Length - 1 ?
							ocs.baseClass : "";
						ocs.baseClassIndex = EditorGUILayout.Popup(s_content_base_class, ocs.baseClassIndex, mBaseClassList);
						if (EditorGUI.EndChangeCheck()) {
							ocs.baseClass = mBaseClassList[ocs.baseClassIndex];
						}
					}
					if (GUILayout.Button(ocs.baseClassIndex < 0 ? s_content_select : s_content_manual, s_layout_width_2)) {
						if (ocs.baseClassIndex < 0) {
							ocs.baseClassIndex = mUsedBaseClasss.IndexOf(ocs.baseClass);
							if (ocs.baseClassIndex < 0) { ocs.baseClassIndex = mUsedBaseClasss.Count; }
						} else {
							ocs.baseClassIndex = -1;
						}
					}
					if (i == 0 && GUILayout.Button(s_content_delete, s_layout_width_2)) {
						if (ocs.baseClassIndex < mBaseClassList.Length - 1) {
							mUsedBaseClasss.RemoveAt(ocs.baseClassIndex);
							SaveUsedBaseClasses();
							ResetBaseClassList();
						}
						ocs.baseClass = mBaseClassList[ocs.baseClassIndex];
					}
					EditorGUILayout.EndHorizontal();
				}
				EditorGUILayout.EndVertical();
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				if (indent > 0) {
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.EndVertical();
			GUILayout.Space(4f);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndScrollView();
			EditorGUILayout.BeginHorizontal();
			EditorGUI.BeginDisabledGroup(mObj == null || mRootComponents == null);
			if (GUILayout.Button(s_content_gen_and_mount, s_layout_width_5)) {
				EditorPrefs.SetString(GetKey("prev_folder"), mFolder);
				string folder = mFolder;
				string ns = mNameSpaceList[mNameSpaceIndex];
				if (!mUsedNameSpaces.Contains(ns)) {
					mUsedNameSpaces.Add(ns);
					SaveUsedNameSpaces();
					ResetNameSpaceList();
				}
				bool usedBaseClassesChanged = false;
				for (int i = 0; i < count; i++) {
					ObjectComponents ocs = mDrawingComponents[i].components;
					if (!string.IsNullOrEmpty(ocs.baseClass) && !mUsedBaseClasss.Contains(ocs.baseClass)) {
						mUsedBaseClasss.Add(ocs.baseClass);
						usedBaseClassesChanged = true;
					}
				}
				if (usedBaseClassesChanged) {
					SaveUsedBaseClasses();
					ResetBaseClassList();
				}
				if (!folder.EndsWith("/")) { folder = folder + "/"; }
				List<CodeObject> codes = GetCodes(ns);
				bool flag = false;
				for (int i = 0, imax = codes.Count; i < imax; i++) {
					CodeObject code = codes[i];
					string path = string.Concat(folder, code.filename);
					string fileMd5 = null;
					if (File.Exists(path)) {
						try {
							using (FileStream fs = File.OpenRead(path)) {
								fileMd5 = BitConverter.ToString(md5_calc.ComputeHash(fs));
							}
						} catch (Exception e) { Debug.LogException(e); }
					}
					byte[] bytes = Encoding.UTF8.GetBytes(code.code);
					bool toWrite = true;
					if (!string.IsNullOrEmpty(fileMd5)) {
						if (BitConverter.ToString(md5_calc.ComputeHash(bytes)) == fileMd5) {
							toWrite = false;
						}
					}
					if (toWrite) {
						File.WriteAllBytes(path, bytes);
						flag = true;
					}
				}
				if (flag) {
					AssetDatabase.Refresh();
				}
				if (EditorApplication.isCompiling) {
					EditorPrefs.SetString(GetKey("set_serialized_objects"), mObj.name);
				} else {
					mToSetSerializedObjects = true;
				}
			}
			if (GUILayout.Button(s_content_preview_code, s_layout_width_5)) {
				Rect rect = position;
				rect.x += 32f;
				rect.y += 32f;
				rect.width = 800f;
				CodePreviewWindow pw = GetWindow<CodePreviewWindow>("Code Preview");
				pw.position = rect;
				pw.codes = GetCodes(mNameSpaceList[mNameSpaceIndex]);
				pw.Show();
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();
			if (mToSetSerializedObjects) {
				mToSetSerializedObjects = false;
				if (SerializeObject(mNameSpaceList[mNameSpaceIndex], mRootComponents)) {
					AssetDatabase.SaveAssets();
				}
			}
		}

		private bool SerializeObject(string ns, ObjectComponents ocs) {
			Queue<ObjectComponents> components = new Queue<ObjectComponents>();
			components.Enqueue(ocs);
			Stack<ObjectComponents> sortedComponents = new Stack<ObjectComponents>();
			while (components.Count > 0) {
				ObjectComponents oc = components.Dequeue();
				sortedComponents.Push(oc);
				for (int i = 0, imax = oc.itemComponents.Count; i < imax; i++) {
					ObjectComponents ioc = oc.itemComponents[i];
					if (ioc.itemComponents != null) { components.Enqueue(ioc); }
				}
			}
			bool ret = false;
			while (sortedComponents.Count > 0) {
				ObjectComponents oc = sortedComponents.Pop();
				//TODO find type
				string typeFullName = string.IsNullOrEmpty(ns) ? oc.cls : string.Concat(ns, ".", oc.cls);
				Type type = null;
				Type ft = null;
				Type tm = typeof(MonoBehaviour);
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					type = Type.GetType(typeFullName + "," + assembly.GetName().Name);
					if (type == null) { continue; }
					if (type.IsSubclassOf(tm)) { break; }
					ft = type;
					type = null;
				}
				if (type == null) {
					if (ft == null) {
						Debug.LogErrorFormat("Cannot Find Type for {0} !", oc.cls);
					} else {
						Debug.LogErrorFormat("Type : '{0}' is not subclass of MonoBehaviour !", ft.FullName);
					}
				} else {
					oc.type = type;
					Component component = oc.go.GetComponent(type);
					if (component == null) {
						component = oc.go.AddComponent(type);
					}
					SerializedObject so = new SerializedObject(component);
					List<ObjectComponents> itemComponents = oc.itemComponents;
					int count = itemComponents.Count;
					for (int i = 0; i < count; i++) {
						ObjectComponents ioc = itemComponents[i];
						SerializedProperty pObj = so.FindProperty(oc.publicProperty ? "m_" + ioc.name : ioc.name);
						if (pObj == null) {
							Debug.LogErrorFormat(ioc.go, "Cannot Find property for node '{0}' !", ioc.go.name);
							continue;
						}
						SerializedProperty pGO = pObj.FindPropertyRelative("m_GameObject");
						pGO.objectReferenceValue = ioc.go;
						int cCount = ioc.Count;
						for (int j = 0; j < cCount; j++) {
							ComponentData cd = ioc[j];
							SerializedProperty pComponent = pObj.FindPropertyRelative("m_" + cd.type.variableName);
							if (pComponent == null) {
								Debug.LogErrorFormat(cd.component, "Cannot Find property for Component '{0}' at '{1}' !",
									cd.type.variableName, ioc.go.name);
								continue;
							}
							pComponent.objectReferenceValue = cd.component;
						}
						if (ioc.itemComponents != null && ioc.type != null) {
							SerializedProperty pItem = pObj.FindPropertyRelative("m_" + ioc.clsVar);
							if (pItem == null) {
								Debug.LogErrorFormat(ioc.go, "Cannot Find item property for Component '{0}' at '{1}' !",
									ioc.type.Name, ioc.go.name);
							} else {
								pItem.objectReferenceValue = ioc.go.GetComponent(ioc.type);
							}
						}
					}
					if (so.ApplyModifiedProperties()) { EditorUtility.SetDirty(component); ret = true; }
				}
			}
			return ret;
		}

		private void ResetFolderIndex() {
			mFolderIndex = 0;
			for (int i = mFolderList.Length - 1; i >= 0; i--) {
				if (mFolder == mFolderList[i]) {
					mFolderIndex = i;
					break;
				}
			}
		}

		private List<CodeObject> GetCodes(string ns) {
			List<CodeObject> codes = new List<CodeObject>();
			if (mRootComponents == null) { return codes; }
			List<ClassData> clses = new List<ClassData>();
			Queue<ObjectComponents> components = new Queue<ObjectComponents>();
			components.Enqueue(mRootComponents);
			while (components.Count > 0) {
				ObjectComponents ocs = components.Dequeue();
				List<ObjectComponents> itemComponents = ocs.itemComponents;
				if (itemComponents == null) { continue; }
				ClassData cd = null;
				for (int i = clses.Count - 1; i >= 0; i--) {
					if (clses[i].cls == ocs.cls) {
						cd = clses[i];
						break;
					}
				}
				if (cd == null) {
					cd = new ClassData();
					cd.cls = ocs.cls;
					clses.Add(cd);
				}
				if (!GetClass(ocs, cd)) {
					// TODO class not match...
				}
				/*string code = GetCode(ns, ocs);
				if (!string.IsNullOrEmpty(code)) {
					codes.Add(new CodeObject(string.Concat(ocs.cls, ".cs"), code));
				}*/
				for (int i = 0, imax = itemComponents.Count; i < imax; i++) {
					ObjectComponents ioc = itemComponents[i];
					if (ioc.itemComponents == null) { continue; }
					components.Enqueue(ioc);
				}
			}
			Comparison<SupportedTypeData> typeSorter = SortSupportedTypeDatas;
			for (int i = 0, imax = clses.Count; i < imax; i++) {
				ClassData cls = clses[i];
				for (int j = cls.fields.Count - 1; j >= 0; j--) {
					FieldData field = cls.fields[j];
					if (!string.IsNullOrEmpty(field.itemType)) {
						for (int k = 0; k < imax; k++) {
							ClassData cd = clses[k];
							if (field.itemType == cd.cls) {
								field.itemClass = cd;
							}
						}
					}
				}
			}
			for (int i = 0, imax = clses.Count; i < imax; i++) {
				ClassData cls = clses[i];
				for (int j = cls.fields.Count - 1; j >= 0; j--) {
					cls.fields[j].components.Sort(typeSorter);
				}
				string code = GetCode(ns, clses[i]);
				if (!string.IsNullOrEmpty(code)) {
					codes.Add(new CodeObject(string.Concat(cls.cls, ".cs"), code));
				}
			}
			return codes;
		}

		private class ClassData {
			public List<string> usings = new List<string>();
			public string cls;
			public string baseClass;
			public bool partialClass;
			public bool publicProperty;
			public List<FieldData> fields = new List<FieldData>();
			public bool HasClear() {
				for (int i = fields.Count - 1; i >= 0; i--) {
					FieldData field = fields[i];
					if (!string.IsNullOrEmpty(field.itemType)) { return true; }
					if (field.HasClear()) { return true; }
				}
				return false;
			}
		}
		private class FieldData {
			public string name;
			public string itemType;
			public ClassData itemClass;
			public string itemVar;
			public List<SupportedTypeData> components = new List<SupportedTypeData>();
			public bool HasClear() {
				for (int j = components.Count - 1; j >= 0; j--) {
					SupportedTypeData comp = components[j];
					string[] cc = comp.GetClearCalls();
					if (cc != null && cc.Length > 0) { return true; }
				}
				return false;
			}
		}

		private static bool GetClass(ObjectComponents ocs, ClassData cls) {
			if (cls.cls != ocs.cls) { return false; }
			if (cls.baseClass != null && cls.baseClass != ocs.baseClass) { return false; }
			cls.baseClass = ocs.baseClass;
			cls.partialClass |= ocs.partialClass;
			cls.publicProperty |= ocs.publicProperty;
			List<ObjectComponents> objComponents = ocs.itemComponents;
			for (int i = 0, imax = objComponents.Count; i < imax; i++) {
				ObjectComponents oc = objComponents[i];
				FieldData field = null;
				for (int j = cls.fields.Count - 1; j >= 0; j--) {
					FieldData f = cls.fields[j];
					if (f.name == oc.name) {
						field = f;
						break;
					}
				}
				if (field == null) {
					field = new FieldData();
					field.name = oc.name;
					field.itemType = null;
					cls.fields.Add(field);
				}
				for (int j = 0, jmax = oc.Count; j < jmax; j++) {
					SupportedTypeData type = oc[j].type;
					for (int k = field.components.Count - 1; k >= 0; k--) {
						if (field.components[k].showName == type.showName) {
							type = null;
							break;
						}
					}
					if (type != null) { field.components.Add(type); }
				}
				if (oc.itemComponents != null) {
					string itemClass = oc.cls;
					for (int k = field.components.Count - 1; k >= 0; k--) {
						if (field.components[k].showName == itemClass) {
							itemClass = null;
							break;
						}
					}
					if (itemClass != null) {
						field.components.Add(new SupportedTypeData(null, 10000, oc.cls, null, oc.cls, oc.clsVar, true, false));
						field.itemType = oc.cls;
						field.itemVar = oc.clsVar;
					}
				}
			}
			return true;
		}

		private static string GetCode(string ns, ClassData cls) {
			List<string> usings = new List<string>();
			usings.Add(typeof(UnityEvent).Namespace);
			SortedList<string, SupportedTypeData[]> dataClasses = new SortedList<string, SupportedTypeData[]>();
			StringBuilder code = new StringBuilder();
			List<string> clearInvokes = new List<string>();
			code.AppendLine("#pragma warning disable 649");
			code.AppendLine();
			Dictionary<string, KeyValuePair<string, string>> itemClasses = new Dictionary<string, KeyValuePair<string, string>>();
			string codeIndent = "";
			if (!string.IsNullOrEmpty(ns)) {
				codeIndent = "\t";
				code.AppendLine(string.Format("namespace {0} {{", ns));
				code.AppendLine();
			}
			code.AppendLine(string.Format("{0}public {1}class {2} : {3} {{",
				codeIndent, cls.partialClass ? "partial " : "", cls.cls, cls.baseClass));
			code.AppendLine();
			List<string> tempStrings = new List<string>();
			for (int i = 0, imax = cls.fields.Count; i < imax; i++) {
				FieldData field = cls.fields[i];
				tempStrings.Clear();
				for (int j = 0, jmax = field.components.Count; j < jmax; j++) {
					SupportedTypeData typeData = field.components[j];
					tempStrings.Add(typeData.type == null ? typeData.codeTypeName : typeData.type.Name);
					if (!string.IsNullOrEmpty(typeData.nameSpace) && !usings.Contains(typeData.nameSpace)) {
						usings.Add(typeData.nameSpace);
					}
					if (field.itemClass == null) {
						string[] clearCalls = typeData.GetClearCalls();
						if (clearCalls != null && clearCalls.Length > 0) {
							string objstr = field.name + "." + typeData.variableName + "?.";
							foreach (string cc in clearCalls) {
								clearInvokes.Add(objstr + cc);
							}
						}
					} else if (typeData.type == null && field.itemClass.HasClear()) {
						string objstr = field.name + "." + typeData.variableName + "?.";
						clearInvokes.Add(objstr + "Clear()");
					}
				}
				string objTypeName = string.Concat(string.Join("_", tempStrings.ToArray()), "_Container");
				if (!dataClasses.ContainsKey(objTypeName)) {
					dataClasses.Add(objTypeName, field.components.ToArray());
				}
				tempStrings.Clear();
				code.AppendLine(string.Format("{0}\t[SerializeField]", codeIndent));
				if (cls.publicProperty) {
					code.AppendLine(string.Format("{0}\tprivate {1} m_{2};", codeIndent, objTypeName, field.name));
					code.AppendLine(string.Format("{0}\tpublic {1} {2} {{ get {{ return m_{2}; }} }}",
						codeIndent, objTypeName, field.name));
				} else {
					code.AppendLine(string.Format("{0}\tprivate {1} {2};", codeIndent, objTypeName, field.name));
				}
				code.AppendLine();
				if (!string.IsNullOrEmpty(field.itemType) && !string.IsNullOrEmpty(field.itemVar) && !itemClasses.ContainsKey(objTypeName)) {
					itemClasses.Add(objTypeName, new KeyValuePair<string, string>(field.itemType, field.itemVar));
				}
				if (field.itemClass != null) {
					clearInvokes.Add(field.name + "." + "CacheAll()");
				}
			}
			code.AppendLine(string.Format("{0}\tprivate UnityEvent mOnClear;", codeIndent));
			code.AppendLine(string.Format("{0}\tpublic UnityEvent onClear {{", codeIndent));
			code.AppendLine(string.Format("{0}\t\tget {{", codeIndent));
			code.AppendLine(string.Format("{0}\t\t\tif (mOnClear == null) {{ mOnClear = new UnityEvent(); }}", codeIndent));
			code.AppendLine(string.Format("{0}\t\t\treturn mOnClear;", codeIndent));
			code.AppendLine(string.Format("{0}\t\t}}", codeIndent));
			code.AppendLine(string.Format("{0}\t}}", codeIndent));
			code.AppendLine();
			code.AppendLine(string.Format("{0}\tpublic void Clear() {{", codeIndent));
			foreach (string ci in clearInvokes) {
				code.AppendLine(string.Format("{0}\t\t{1};", codeIndent, ci));
			}
			code.AppendLine(string.Format("{0}\t\tif (mOnClear != null) {{ mOnClear.Invoke(); mOnClear.RemoveAllListeners(); }}", codeIndent));
			code.AppendLine(string.Format("{0}\t}}", codeIndent));
			code.AppendLine();
			foreach (KeyValuePair<string, SupportedTypeData[]> kv in dataClasses) {
				code.AppendLine(string.Format("{0}\t[System.Serializable]", codeIndent));
				code.AppendLine(string.Format("{0}\t{1} class {2} {{",
					codeIndent, cls.publicProperty ? "public" : "private", kv.Key));
				code.AppendLine();
				code.AppendLine(string.Format("{0}\t\t[SerializeField]", codeIndent));
				code.AppendLine(string.Format("{0}\t\tprivate GameObject m_GameObject;", codeIndent));
				code.AppendLine(string.Format("{0}\t\tpublic GameObject gameObject {{ get {{ return m_GameObject; }} }}", codeIndent));
				code.AppendLine();
				for (int i = 0, imax = kv.Value.Length; i < imax; i++) {
					SupportedTypeData typeData = kv.Value[i];
					code.AppendLine(string.Format("{0}\t\t[SerializeField]", codeIndent));
					code.AppendLine(string.Format("{0}\t\tprivate {1} m_{2};",
						codeIndent, typeData.codeTypeName, typeData.variableName));
					code.AppendLine(string.Format("{0}\t\tpublic {1} {2} {{ get {{ return m_{2}; }} }}",
						codeIndent, typeData.codeTypeName, typeData.variableName));
					code.AppendLine();
				}
				KeyValuePair<string, string> typeAndVar;
				if (itemClasses.TryGetValue(kv.Key, out typeAndVar)) {
					code.AppendLine(string.Format("{0}\t\tprivate Queue<{1}> mCachedInstances;", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\tprivate List<{1}> mUsingInstances;", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\tpublic {1} GetInstance() {{", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\t\t{1} instance = null;", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\t\tif (mCachedInstances != null) {{", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\twhile ((instance == null || instance.Equals(null)) && mCachedInstances.Count > 0) {{", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t\tinstance = mCachedInstances.Dequeue();", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t}}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t}}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tif (instance == null || instance.Equals(null)) {{", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\tinstance = Instantiate<{1}>(m_{2});", codeIndent, typeAndVar.Key, typeAndVar.Value));
					code.AppendLine(string.Format("{0}\t\t\t}}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tTransform t0 = m_{1}.transform;", codeIndent, typeAndVar.Value));
					code.AppendLine(string.Format("{0}\t\t\tTransform t1 = instance.transform;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tt1.SetParent(t0.parent);", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tt1.localPosition = t0.localPosition;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tt1.localRotation = t0.localRotation;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tt1.localScale = t0.localScale;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tt1.SetSiblingIndex(t0.GetSiblingIndex() + 1);", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tif (mUsingInstances == null) {{ mUsingInstances = new List<{1}>(); }}", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\t\tmUsingInstances.Add(instance);", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\treturn instance;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t}}", codeIndent));
					code.AppendLine(string.Format("{0}\t\tpublic bool CacheInstance({1} instance) {{", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\t\tif (instance == null || instance.Equals(null)) {{ return false; }}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tif (mUsingInstances == null || !mUsingInstances.Remove(instance)) {{ return false; }}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tif (mCachedInstances == null) {{ mCachedInstances = new Queue<{1}>(); }}", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\t\tinstance.Clear();", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tinstance.gameObject.SetActive(false);", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tmCachedInstances.Enqueue(instance);", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\treturn true;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t}}", codeIndent));
					code.AppendLine(string.Format("{0}\t\tpublic int CacheAll() {{", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tif (mUsingInstances == null) {{ return 0; }}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tif (mCachedInstances == null) {{ mCachedInstances = new Queue<{1}>(); }}", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\t\tint ret = 0;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tfor (int i = mUsingInstances.Count - 1; i >= 0; i--) {{", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t{1} instance = mUsingInstances[i];", codeIndent, typeAndVar.Key));
					code.AppendLine(string.Format("{0}\t\t\t\tif (instance != null && !instance.Equals(null)) {{", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t\tinstance.Clear();", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t\tinstance.gameObject.SetActive(false);", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t\tmCachedInstances.Enqueue(instance);", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t\tret++;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t\t}}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\t}}", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\tmUsingInstances.Clear();", codeIndent));
					code.AppendLine(string.Format("{0}\t\t\treturn ret;", codeIndent));
					code.AppendLine(string.Format("{0}\t\t}}", codeIndent));
					code.AppendLine();
					if (!usings.Contains("System.Collections.Generic")) { usings.Add("System.Collections.Generic"); }
				}
				code.AppendLine(string.Format("{0}\t}}", codeIndent));
				code.AppendLine();
			}
			code.AppendLine(string.Format("{0}}}", codeIndent));
			if (!string.IsNullOrEmpty(ns)) {
				code.AppendLine();
				code.AppendLine("}");
			}
			if (!usings.Contains("UnityEngine")) { usings.Add("UnityEngine"); }
			if (!string.IsNullOrEmpty(ns)) { usings.Remove(ns); }
			usings.Sort();
			StringBuilder codeUsings = new StringBuilder();
			for (int i = 0, imax = usings.Count; i < imax; i++) {
				codeUsings.AppendLine(string.Format("using {0};", usings[i]));
			}
			codeUsings.AppendLine();
			return codeUsings.ToString() + code.ToString();
		}

		private void SaveUsedNameSpaces() {
			string ns = string.Join("|", mUsedNameSpaces.ToArray());
			EditorPrefs.SetString(GetKey("saved_namespaces"), ns);
			EditorPrefs.SetInt(GetKey("saved_namespace_index"), mNameSpaceIndex);
		}

		private void ResetNameSpaceList() {
			mUsedNameSpaces.Clear();
			string savedNameSpaces = EditorPrefs.GetString(GetKey("saved_namespaces"), null);
			mNameSpaceIndex = EditorPrefs.GetInt(GetKey("saved_namespace_index"), 0);
			if (!string.IsNullOrEmpty(savedNameSpaces)) {
				mUsedNameSpaces.AddRange(savedNameSpaces.Split('|'));
			}
			if (mUsedNameSpaces.Count <= 0) {
				mNameSpaceManualEdit = true;
			}
			string editingNameSpace = "";
			if (mNameSpaceList != null && mNameSpaceList.Length > 0) {
				editingNameSpace = mNameSpaceList[mNameSpaceList.Length - 1];
			}
			mUsedNameSpaces.Add(editingNameSpace);
			mNameSpaceList = mUsedNameSpaces.ToArray();
			int namespaceCount = mUsedNameSpaces.Count;
			mUsedNameSpaces.RemoveAt(namespaceCount - 1);
			if (mNameSpaceIndex < 0) {
				mNameSpaceIndex = 0;
			} else if (mNameSpaceIndex >= namespaceCount) {
				mNameSpaceIndex = namespaceCount - 1;
			}
		}

		private void SaveUsedBaseClasses() {
			string classes = string.Join("|", mUsedBaseClasss.ToArray());
			EditorPrefs.SetString(GetKey("saved_baseclasses"), classes);
		}

		private void ResetBaseClassList() {
			mUsedBaseClasss.Clear();
			string savedBaseClasss = EditorPrefs.GetString(GetKey("saved_baseclasses"), null);
			if (!string.IsNullOrEmpty(savedBaseClasss)) {
				mUsedBaseClasss.AddRange(savedBaseClasss.Split('|'));
			}
			if (mUsedBaseClasss.Count <= 0) {
				mUsedBaseClasss.Add("MonoBehaviour");
			}
			mUsedBaseClasss.Add("");
			mBaseClassList = mUsedBaseClasss.ToArray();
			int baseclassCount = mUsedBaseClasss.Count;
			mUsedBaseClasss.RemoveAt(baseclassCount - 1);
		}

		private struct ComponentData {
			public SupportedTypeData type;
			public Component component;
		}

		private struct ObjectComponentsWithIndent {
			public int indent;
			public ObjectComponents components;
		}

		private class ObjectComponents {
			public readonly string name;
			public readonly GameObject go;
			public readonly string cls;
			public readonly string clsVar;
			public readonly List<ObjectComponents> itemComponents;
			public int Count { get { return mComponents.Count; } }
			public bool AbortChild { get; private set; }
			public int baseClassIndex;
			public string baseClass = "MonoBehaviour";
			public bool partialClass;
			public bool publicProperty;
			public Type type;
			public ComponentData this[int i] {
				get { return mComponents[i]; }
			}
			public ObjectComponents(GameObject go, string name, string cls, string clsVar, List<ObjectComponents> itemComponents) {
				this.go = go;
				this.name = name;
				this.cls = cls;
				this.clsVar = clsVar;
				this.itemComponents = itemComponents;
				AbortChild = false;
				temp_components.Clear();
				go.GetComponents<Component>(temp_components);
				for (int i = 0, imax = temp_components.Count; i < imax; i++) {
					Component component = temp_components[i];
					if (component == null || component.Equals(null)) { continue; }
					if (itemComponents != null && !(component is Transform)) { continue; }
					SupportedTypeData std = GetSupportedTypeData(component.GetType());
					if (std == null) { continue; }
					ComponentData data = new ComponentData();
					data.type = std;
					data.component = component;
					mComponents.Add(data);
					if (std.abortChild) { AbortChild = true; }
				}
				temp_components.Clear();
			}
			private List<ComponentData> mComponents = new List<ComponentData>();
			private static List<Component> temp_components = new List<Component>();
		}

		private class CodeObject {
			public readonly string filename;
			public readonly string code;
			public readonly GUIContent codeContent;
			public CodeObject(string filename, string code) {
				this.filename = filename;
				this.code = code;
				codeContent = new GUIContent(code);
			}
		}

		private class CodePreviewWindow : EditorWindow {
			public List<CodeObject> codes;
			private Vector2[] mScrolls;
			private bool mStyleInited = false;
			private GUIStyle mStyleBox;
			private GUIStyle mStyleMessage;
			void OnGUI() {
				if (!mStyleInited) {
					mStyleInited = true;
					mStyleBox = GUI.skin.FindStyle("OL Box") ?? GUI.skin.FindStyle("CN Box");
					mStyleMessage = "CN Message";
				}
				if (codes != null) {
					if (mScrolls == null || mScrolls.Length != codes.Count) {
						mScrolls = new Vector2[codes.Count];
					}
					for (int i = 0, imax = codes.Count; i < imax; i++) {
						CodeObject code = codes[i];
						EditorGUILayout.LabelField(code.filename);
						GUILayout.Space(2f);
						EditorGUILayout.BeginHorizontal();
						GUILayout.Space(4f);
						EditorGUILayout.BeginVertical(mStyleBox);
						mScrolls[i] = EditorGUILayout.BeginScrollView(mScrolls[i], false, false);
						Rect rect = GUILayoutUtility.GetRect(code.codeContent, mStyleBox);
						EditorGUI.SelectableLabel(rect, code.code, mStyleMessage);
						EditorGUILayout.EndScrollView();
						EditorGUILayout.EndVertical();
						EditorGUILayout.EndHorizontal();
					}
				}
			}
		}

	}

}