using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;

namespace GreatClock.Common.SerializeTools {

	public delegate SupportedTypeData DefineSupportedTypeDelegate();

	[AttributeUsage(AttributeTargets.Method)]
	public class SupportedComponentTypeAttribute : Attribute { }

	public class SupportedTypeData {

		public readonly Type type;
		public readonly int priority;
		public readonly string showName;
		public readonly string nameSpace;
		public readonly string codeTypeName;
		public readonly string variableName;
		public readonly bool requireClearOnRecycle;

		public SupportedTypeData(Type type, int priority, string showName, string nameSpace, string codeTypeName, string variableName, bool requireClearOnRecycle = false) {
			this.type = type;
			this.priority = priority;
			this.showName = showName;
			this.nameSpace = nameSpace;
			this.codeTypeName = codeTypeName;
			this.variableName = variableName;
			this.requireClearOnRecycle = requireClearOnRecycle;
		}

		private string[] mClearCalls = null;
		public string[] GetClearCalls() {
			if (mClearCalls == null) {
				s_temp_strings.Clear();
				if (requireClearOnRecycle) {
					BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
					foreach (MemberInfo member in type.GetMembers(flags)) {
						Type fieldType = null;
						PropertyInfo pi = member as PropertyInfo;
						FieldInfo fi = member as FieldInfo;
						if (pi != null) { fieldType = pi.PropertyType; }
						if (fi != null) { fieldType = fi.FieldType; }
						if (fieldType == null || !fieldType.IsSubclassOf(s_type_event_base)) { continue; }
						if (member.GetCustomAttribute<ObsoleteAttribute>() != null) { continue; }
						s_temp_strings.Add($"{member.Name}.RemoveAllListeners()");
					}
					s_temp_strings.Sort();
				}
				mClearCalls = s_temp_strings.ToArray();
				s_temp_strings.Clear();
			}
			return mClearCalls;
		}

		private static Type s_type_event_base = typeof(UnityEventBase);
		private static List<string> s_temp_strings = new List<string>();

	}

}