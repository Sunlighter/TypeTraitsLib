using System;

namespace Sunlighter.TypeTraitsLib.Building
{

	[Serializable]
	public class BuilderException : Exception
	{
		public BuilderException() { }
		public BuilderException(string message) : base(message) { }
		public BuilderException(string message, Exception inner) : base(message, inner) { }

#if !NETSTANDARD2_0
		[Obsolete]
#endif
		protected BuilderException
		(
		    System.Runtime.Serialization.SerializationInfo info,
		    System.Runtime.Serialization.StreamingContext context
	    )
			: base(info, context)
		{
		}
	}
}
