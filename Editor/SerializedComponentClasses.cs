using System;

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
		public SupportedTypeData(Type type, int priority, string showName, string nameSpace, string codeTypeName, string variableName) {
			this.type = type;
			this.priority = priority;
			this.showName = showName;
			this.nameSpace = nameSpace;
			this.codeTypeName = codeTypeName;
			this.variableName = variableName;
		}
	}

}