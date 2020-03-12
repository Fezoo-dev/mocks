using System;
using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace MockFramework
{
    public class ThingCache
    {
        private readonly IDictionary<string, Thing> dictionary
            = new Dictionary<string, Thing>();
        private readonly IThingService thingService;

        public ThingCache(IThingService thingService)
        {
            this.thingService = thingService;
        }

        public Thing Get(string thingId)
        {
            Thing thing;
            if (dictionary.TryGetValue(thingId, out thing))
                return thing;
            if (thingService.TryRead(thingId, out thing))
            {
                dictionary[thingId] = thing;
                return thing;
            }
            return null;
        }
    }

    [TestFixture]
    public class ThingCache_Should
    {
        private IThingService thingService;
        private ThingCache thingCache;

        private const string thingId1 = "TheDress";
        private Thing thing1 = new Thing(thingId1);

        private const string thingId2 = "CoolBoots";
        private Thing thing2 = new Thing(thingId2);

        [SetUp]
        public void SetUp()
        {
            thingService = A.Fake<IThingService>();
            thingCache = new ThingCache(thingService);
        }

        [Test]
        public void Get_NoneExistingObject_ReturnsNull()
        {
            thingCache.Get(thingId1)
                .Should().BeNull();

            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .MustHaveHappened();
        }

        [Test]
        public void Get_ReturnsObject_WhenObjectNotInCache()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .Returns(true);

            thingCache.Get(thingId1)
                .Should().Be(thing1);
        }

        [Test]
        public void Get_ReturnsCachedObject_WhenObjectInCache()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .Returns(true);

            thingCache.Get(thingId1);
            thingCache.Get(thingId1)
                .Should()
                .Be(thing1);

            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void Get_CacheEachNewObject()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .Returns(true);
            A.CallTo(() => thingService.TryRead(thingId2, out thing2))
                .Returns(true);

            thingCache.Get(thingId1);
            thingCache.Get(thingId2);

            Thing result;
            A.CallTo(() => thingService.TryRead(A<string>.Ignored, out result))
                .MustHaveHappened(Repeated.Exactly.Twice);
        }

        [Test]
        public void Get_ReturnNullTwice_WhenRequestingNonExistingObjectTwice()
        {
            thingCache.Get(String.Empty);
            thingCache.Get(String.Empty);

            Thing tmp;
            A.CallTo(() => thingService.TryRead(A<string>.Ignored, out tmp))
                .MustHaveHappened(Repeated.Exactly.Twice);
        }

        [Test]
        public void Get_ThrowArgumentNullException_WhenThingIdIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => thingCache.Get(null));
        }
    }
}