using System;
using Nop.Core.Infrastructure;
using NUnit.Framework;

namespace Nop.Core.Tests
{
    public abstract class TypeFindingBase
    {
        protected ITypeFinder typeFinder;

        protected abstract Type[] GetTypes();

        [SetUp]
        public void SetUp()
        {
            typeFinder = new Fakes.FakeTypeFinder(typeof(TypeFindingBase).Assembly, GetTypes());
        }
    }
}
