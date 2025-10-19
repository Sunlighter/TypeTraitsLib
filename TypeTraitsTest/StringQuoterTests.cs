using Sunlighter.TypeTraitsLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeTraitsTest
{
    [TestClass]
    public sealed class StringQuoterTests
    {
        [TestMethod]
        public void TestSurrogateChars()
        {
            int code = 0x1F600; // grinning face emoji
            string str = char.ConvertFromUtf32(code); // verify that this is a surrogate pair
            string quoted = StringTypeTraits.Value.ToDebugString(str);
            System.Diagnostics.Debug.WriteLine($"{str} {quoted}");
            Assert.AreEqual("\"\\x1F600;\"", quoted);
        }
    }
}
