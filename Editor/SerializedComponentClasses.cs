using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;

namespace GreatClock.Common.SerializeTools {

	public delegate SupportedTypeData DefineSupportedTypeDelegate();

	[AttributeUsage(AttributeTargets.Method)]
	public class SupportedComponentTypeAttribute : Attribute { }

	public class SupportedTypeData {

		public Type type { get; private set; }
		public int priority { get; private set; }
		public string showName { get; private set; }
		public string nameSpace { get; private set; }
		public string codeTypeName { get; private set; }
		public string variableName { get; private set; }
		public bool requireClearOnRecycle { get; private set; }
		public bool abortChild { get; private set; }

		public SupportedTypeData(Type type, int priority,
			string showName, string nameSpace, string codeTypeName, string variableName,
			bool requireClearOnRecycle, bool abortChild) {
			this.type = type;
			this.priority = priority;
			this.showName = showName;
			this.nameSpace = nameSpace;
			this.codeTypeName = codeTypeName;
			this.variableName = variableName;
			this.requireClearOnRecycle = requireClearOnRecycle;
			this.abortChild = abortChild;
		}

		public SupportedTypeData(Type type, int priority) {
			this.type = type;
			this.priority = priority;
			showName = null;
			nameSpace = null;
			codeTypeName = null;
			variableName = null;
			requireClearOnRecycle = true;
			abortChild = false;
		}

		public SupportedTypeData SetShowName(string showName) {
			this.showName = showName;
			return this;
		}

		public SupportedTypeData SetNameSpace(string nameSpace) {
			this.nameSpace = nameSpace;
			return this;
		}

		public SupportedTypeData SetCodeTypeName(string codeTypeName) {
			this.codeTypeName = codeTypeName;
			return this;
		}

		public SupportedTypeData SetVariableName(string variableName) {
			this.variableName = variableName;
			return this;
		}

		public SupportedTypeData SetRequireClearOnRecycle(bool requireClearOnRecycle) {
			this.requireClearOnRecycle = requireClearOnRecycle;
			return this;
		}

		public SupportedTypeData SetAbortChild(bool abortChild) {
			this.abortChild = abortChild;
			return this;
		}

		private string[] mClearCalls = null;
		public string[] GetClearCalls() {
			if (mClearCalls == null) {
				s_temp_strings.Clear();
				if (requireClearOnRecycle) {
					BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
					bool hasClear = false;
					foreach (MemberInfo member in type.GetMembers(flags)) {
						MethodInfo mi = member as MethodInfo;
						if (mi != null) {
							if (mi.Name == "Clear" && mi.GetParameters().Length <= 0 && mi.ReturnType == s_type_void) {
								hasClear = true;
							}
							continue;
						}
						Type fieldType = null;
						PropertyInfo pi = member as PropertyInfo;
						FieldInfo fi = member as FieldInfo;
						if (pi != null) { fieldType = pi.PropertyType; }
						if (fi != null) { fieldType = fi.FieldType; }
						if (fieldType == null || !fieldType.IsSubclassOf(s_type_event_base)) { continue; }
						if (member.GetCustomAttribute<ObsoleteAttribute>() != null) { continue; }
						s_temp_strings.Add(string.Format("{0}.RemoveAllListeners()", member.Name));
					}
					s_temp_strings.Sort();
					if (hasClear) { s_temp_strings.Add("Clear()"); }
				}
				mClearCalls = s_temp_strings.ToArray();
				s_temp_strings.Clear();
			}
			return mClearCalls;
		}

		private static Type s_type_void = typeof(void);
		private static Type s_type_event_base = typeof(UnityEventBase);
		private static List<string> s_temp_strings = new List<string>();

	}

}