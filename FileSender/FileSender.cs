using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using FileSender.Dependencies;
using FluentAssertions;
using NUnit.Framework;

namespace FileSender
{
    public class FileSender
    {
        private readonly ICryptographer cryptographer;
        private readonly ISender sender;
        private readonly IRecognizer recognizer;

        public FileSender(
            ICryptographer cryptographer,
            ISender sender,
            IRecognizer recognizer)
        {
            this.cryptographer = cryptographer;
            this.sender = sender;
            this.recognizer = recognizer;
        }

        public Result SendFiles(File[] files, X509Certificate certificate)
        {
            return new Result
            {
                SkippedFiles = files
                    .Where(file => !TrySendFile(file, certificate))
                    .ToArray()
            };
        }

        private bool TrySendFile(File file, X509Certificate certificate)
        {
            Document document;
            if (!recognizer.TryRecognize(file, out document))
                return false;
            if (!CheckFormat(document) || !CheckActual(document))
                return false;
            var signedContent = cryptographer.Sign(document.Content, certificate);
            return sender.TrySend(signedContent);
        }

        private bool CheckFormat(Document document)
        {
            return document.Format == "4.0" ||
                   document.Format == "3.1";
        }

        private bool CheckActual(Document document)
        {
            return document.Created.AddMonths(1) > DateTime.Now;
        }

        public class Result
        {
            public File[] SkippedFiles { get; set; }
        }
    }

    //TODO: реализовать недостающие тесты
    [TestFixture]
    public class FileSender_Should
    {
        private FileSender fileSender;
        private ICryptographer cryptographer;
        private ISender sender;
        private IRecognizer recognizer;

        private readonly X509Certificate certificate = new X509Certificate();
        private File file;
        Document document;
        private DateTime originalDateTime;
        private byte[] signedContent;

        [SetUp]
        public void SetUp()
        {
            // Постарайтесь вынести в SetUp всё неспецифическое конфигурирование так,
            // чтобы в конкретных тестах осталась только специфика теста,
            // без конфигурирования "обычного" сценария работы

            file = new File("someFile", new byte[] {1, 2, 3});
            signedContent = new byte[] {1, 7};

            cryptographer = A.Fake<ICryptographer>();
            sender = A.Fake<ISender>();
            recognizer = A.Fake<IRecognizer>();
            fileSender = new FileSender(cryptographer, sender, recognizer);

            originalDateTime = DateTime.Now;
            document = new Document(file.Name, file.Content, originalDateTime, "4.0");
         
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);
        }

        [TestCase("4.0")]
        [TestCase("3.1")]
        public void Send_WhenGoodFormat(string format)
        {
            document.Format = format;

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [Test]
        public void IndependentlySend_WhenAllFilesAreGood()
        {
            fileSender.SendFiles(new[] {file, file, file}, certificate)
                .SkippedFiles.Should().BeEmpty();

            A.CallTo(() => sender.TrySend(null))
                .WithAnyArguments()
                .MustHaveHappened(Repeated.Exactly.Times(3));
        }

        [TestCase("0.0")]
        [TestCase(null)]
        public void Skip_WhenBadFormat(string format)
        {
            document.Format = format;

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEquivalentTo(file);
        }

        [Test]
        public void Skip_WhenOlderThanAMonth()
        {
            document.Created = originalDateTime.AddMonths(-1).AddSeconds(-1);

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEquivalentTo(file);
        }

        [Test]
        public void Send_WhenYoungerThanAMonth()
        {
            document.Created = originalDateTime.AddSeconds(1);
            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenSendFails()
        {
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(false);

            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().NotBeEmpty();
        }

        [Test]
        public void Skip_WhenNotRecognized()
        {
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(false);

            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().NotBeEmpty();
        }

        [Test]
        public void DoNotTrySendFile_WhenNotRecognized()
        {
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(false);

            fileSender.SendFiles(new[] {file}, certificate);
            A.CallTo(() => sender.TrySend(null))
                .WithAnyArguments()
                .MustNotHaveHappened();
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeAreInvalid()
        {
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(false);

            fileSender.SendFiles(new[] { file, file }, certificate)
                .SkippedFiles.Should().NotBeEmpty();
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeCouldNotSend()
        {
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(false);

            fileSender.SendFiles(new[] { file, file }, certificate)
                .SkippedFiles.Should().NotBeEmpty();
        }
    }
}
