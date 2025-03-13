using System.Linq.Expressions;
using System.Threading.Tasks;
using Contracts.Domain;
using Contracts.DTO;
using Infrastructure.Helpers;
using Infrastructure.Postgres.Context;
using Infrastructure.Postgres.Model;
using Infrastructure.Postgres.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace ProjectTests.Infrastructure.Postgres.Repository
{
    [TestFixture]
    public class UserEntityTests
    {
        private Mock<ILogger<UserEntity>> _loggerMock;
        private AppDbContext _dbContext;
        private UserEntity _userEntity;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<UserEntity>>();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;

            _dbContext = new AppDbContext(options);
            _userEntity = new UserEntity(_loggerMock.Object, _dbContext);
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        [Test]
        public void AddUser_ShouldAddUserToDbContext()
        {
            var userData = new UserData { UserId = 1 };

            _userEntity.AddUser(userData);

            var user = _dbContext.Users.Find(1L);
            Assert.That(user, Is.Not.Null);
            Assert.That(user.UserId, Is.EqualTo(1L));
        }

        [Test]
        public async Task Save_ShouldCallSaveChangesAsync()
        {
            var result = await _userEntity.Save();

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task GetOrCreateUserById_ShouldReturnExistingUser()
        {
            var userId = 1L;
            var userData = new UserData { UserId = userId };
            _dbContext.Users.Add(userData);
            await _dbContext.SaveChangesAsync();

            var result = await _userEntity.GetOrCreateUserById(userId);

            Assert.That(result, Is.EqualTo(userData));
        }

        [Test]
        public async Task GetOrCreateUserById_ShouldCreateNewUserIfNotExists()
        {
            var userId = 1L;

            var result = await _userEntity.GetOrCreateUserById(userId);

            Assert.That(result.UserId, Is.EqualTo(userId));
            var user = _dbContext.Users.Find(userId);
            Assert.That(user, Is.Not.Null);
            Assert.That(user.UserId, Is.EqualTo(userId));
        }

        [Test]
        public async Task UserExists_ShouldReturnTrueIfUserExists()
        {
            var userId = 1L;
            var userData = new UserData { UserId = userId };
            _dbContext.Users.Add(userData);
            await _dbContext.SaveChangesAsync();

            var result = await _userEntity.UserExists(userId);

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task UserExists_ShouldReturnFalseIfUserDoesNotExist()
        {
            var userId = 1L;

            var result = await _userEntity.UserExists(userId);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task StoreLogRecord_ShouldStoreLogRecordCorrectly()
        {
            var logDataMessage = new LogDataMessage(
                LogData: new LogData { UserId = 1, IPAddress = "192.168.1.1", EventId = 100 },
                DateTime: DateTimeOffset.UtcNow);

            var result = await _userEntity.StoreLogRecord(logDataMessage);

            var user = _dbContext.Users.Find(1L);
            Assert.Multiple(() =>
            {
                Assert.That(result.UserData, Is.EqualTo(user));
                Assert.That(result.ConnectionDataV4, Is.Not.Null);
            });
            Assert.That(result.ConnectionDataV4.IpAddress, Is.EqualTo(IpHelper.IPAddressInt32(logDataMessage.LogData.IPAddress)));
        }
    }
}
