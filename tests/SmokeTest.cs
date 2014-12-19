namespace MuMech
{
    using NUnit.Framework;

    // making sure I can run tests
    [TestFixture]
        public class SmokeTest
        {
            [Test]
                public void CheckName()
                {
                    OperationCircularize oc = new OperationCircularize();
                    Assert.AreEqual("circularize", oc.getName());
                }

        }

}
