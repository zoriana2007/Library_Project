using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests
{
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        /// <summary>
        /// Testing: Adding a new book saves it to the repository exactly once.
        /// </summary>
        [Fact]
        public void AddBook_NewBook_SavesBook()
        {
            // Arrange
            const string BOOK_NAME="Pride and Prejudice";
            const int COPIES_COUNT=2;

            _repoMock.Setup(r => r.FindBook(BOOK_NAME)).Returns((Book)null);

            // Act
            _service.AddBook(BOOK_NAME, COPIES_COUNT);

            // Assert
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == BOOK_NAME && b.Copies == COPIES_COUNT)), Times.Once);
        }

        /// <summary>
        /// Testing: Adding an existing book increases its number of copies.
        /// </summary>
        [Fact]
        public void AddBook_ExistingBook_IncreasesCopies()
        {
            //Arrange
            const string BOOK_NAME="Pride and Prejudice";
            const int COPIES_COUNT=2;
            const int COPIES_COUNT_ADDED=4;
            const int COPIES_COUNT_EXPECTED=6;
            
            var fakeBook=new Book
            {
                Title=BOOK_NAME,
                Copies=COPIES_COUNT
            };

            _repoMock.Setup(r => r.FindBook(fakeBook.Title)).Returns(fakeBook);

            //Act
            _service.AddBook(fakeBook.Title, COPIES_COUNT_ADDED);
            
            //Assert
            Assert.Equal(COPIES_COUNT_EXPECTED, fakeBook.Copies);
        }

        /// <summary>
        /// Testing: Adding a book with an invalid title or copy count throws ArgumentException.
        /// </summary>
        [Theory]
        [InlineData(null, 1)]
        [InlineData ("", 2)]
        [InlineData(" ", 3)]
        [InlineData ("Mansfield Park",0)]
        [InlineData ("Pride and Prejudice", -3)]
        public void AddBook_InvalidTitleOrCopies_ThrowsArgumentException(string title, int copies)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.AddBook(title, copies)); 
        }

        /// <summary>
        /// Testing: Borrowing an available book with a valid member returns true
        /// and saves the updated book.
        /// </summary>
        [Fact]
        public void BorrowBook_ValidMemberAndAvailableBook_ReturnsTrueAndSavesBook()
        {
            //Arrange
            const int MEMBER_ID=1;
            const string BOOK_NAME="Pride and Prejudice";
            const int COPIES_COUNT=3;

            var fakeBook=new Book
            {
                Title=BOOK_NAME,
                Copies=COPIES_COUNT
            };
            _memberMock.Setup(s=>s.IsValidMember(MEMBER_ID)).Returns(true);
            _repoMock.Setup(r=>r.FindBook(fakeBook.Title)).Returns(fakeBook);

            //Act
            var result=_service.BorrowBook(MEMBER_ID,fakeBook.Title);

            //Assert
            Assert.True(result);
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Copies == 2)), Times.Once);
        }

        /// <summary>
        /// Testing: Borrowing an available book decreases its number of copies.
        /// </summary>
        [Fact]
        public void BorrowBook_CopiesShouldNotEqualOriginal_AfterBorrow()
        {
            //Arrange
            const int MEMBER_ID=1;
            const string BOOK_NAME="Pride and Prejudice";
            const int COPIES_COUNT=3;

            var fakeBook=new Book
            {
                Title=BOOK_NAME,
                Copies=COPIES_COUNT
            };
            _memberMock.Setup(s=>s.IsValidMember(MEMBER_ID)).Returns(true);
            _repoMock.Setup(r=>r.FindBook(fakeBook.Title)).Returns(fakeBook);

            //Act
            _service.BorrowBook(MEMBER_ID,fakeBook.Title);

            //Assert
            Assert.NotEqual(COPIES_COUNT, fakeBook.Copies);
        }

        /// <summary>
        /// Testing: Borrowing a book with an invalid member
        /// throws InvalidOperationException.
        /// </summary>
        [Fact]
         public void BorrowBook_InvalidMember_ThrowsInvalidOperationException()
        {
            //Arrange
            const int MEMBER_ID=1;
            const string BOOK_NAME="Pride and Prejudice";

            _memberMock.Setup(s=>s.IsValidMember(MEMBER_ID)).Returns(false);

            //Act & Assert
            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(MEMBER_ID, BOOK_NAME));
        }

        /// <summary>
        /// Testing: Borrowing a book that does not exist returns false
        /// and does not send a notification to the member.
        /// </summary>
        [Fact]
        public void BorrowBook_BookNotFound_ReturnsFalseAndDoesNotNotify()
        {
            //Arrange
            const int MEMBER_ID=1;
            const string BOOK_NAME = "Pride and Prejudice";

            _memberMock.Setup(s => s.IsValidMember(MEMBER_ID)).Returns(true);
            _repoMock.Setup(r => r.FindBook(BOOK_NAME)).Returns((Book)null);

             // Act
            var result = _service.BorrowBook(MEMBER_ID, BOOK_NAME);

            //Assert
            Assert.False(result);
            _notifMock.Verify(n => n.NotifyBorrow(MEMBER_ID, BOOK_NAME),Times.Never);
        }

        /// <summary>
        /// Testing: Borrowing a book with no available copies returns false.
        /// </summary>
        [Theory]
        [InlineData (1,-3)]
        [InlineData (5,0)]
        public void BorrowBook_NoAvailableCopies_ReturnsFalse(int memberId, int copies)
        {
            //Arrange
            const string BOOK_NAME="Pride and Prejudice";

            var fakeBook=new Book
            {
                Title=BOOK_NAME,
                Copies=copies
            };
            _memberMock.Setup(s => s.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(fakeBook.Title)).Returns(fakeBook);

             // Act
            var result = _service.BorrowBook(memberId, fakeBook.Title);

            //Assert
            Assert.False(result);
        }

        /// <summary>
        /// Testing: Returning an existing book returns true and notifies the member.
        /// </summary>
        [Fact]
        public void ReturnBook_ExistingBook_ReturnsTrueAndSendsNotification()
        {
            //Arrange
            const int MEMBER_ID=1;
            const string BOOK_NAME="Pride and Prejudice";
            const int COPIES_COUNT=3;

            var fakeBook=new Book
            {
                Title=BOOK_NAME,
                Copies=COPIES_COUNT
            };
            _repoMock.Setup(r => r.FindBook(fakeBook.Title)).Returns(fakeBook);

            //Act
            var result=_service.ReturnBook(MEMBER_ID, fakeBook.Title);

            //Assert
            Assert.True(result);
            _notifMock.Verify(n => n.NotifyReturn(MEMBER_ID, fakeBook.Title),Times.Once);
        }

        /// <summary>
        /// Testing: Returning a book that does not exist returns false 
        /// and does not save a book.
        /// </summary>
        [Fact]
        public void ReturnBook_BookNotFound_ReturnsFalseAndDoesNotSaveBook()
        {
            //Arrange
            const int MEMBER_ID=1;
            const string BOOK_NAME = "Pride and Prejudice";

            _repoMock.Setup(r => r.FindBook(BOOK_NAME)).Returns((Book)null);

            // Act
            var result = _service.ReturnBook(MEMBER_ID, BOOK_NAME);

            //Assert
            Assert.False(result);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
        }

        /// <summary>
        /// Testing: Retrieving available books returns books with available copies.
        /// </summary>
         [Fact]
        public void GetAvailableBooks_AvailableBooksExist_ContainsAvailableBook()
        {
            //Arrange
            var books = new List<Book>
            {
                new Book { Title = "Mansfield Park", Copies = 3 },
                new Book { Title = "Pride and Prejudice", Copies = 1 },
            };

            _repoMock.Setup(r => r.GetAllBooks()).Returns(books);

            // Act
            var result = _service.GetAvailableBooks();

            //Assert
            Assert.Contains(result, b => b.Title == "Mansfield Park");
        }

        /// <summary>
        /// Testing: Retrieving available books returns an empty list
        /// when no books have available copies.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_NoAvailableBooks_ReturnsEmptyList()
        {
            // Arrange
            var books = new List<Book>
            {
                new Book { Title = "Mansfield Park", Copies = 0 },
                new Book { Title = "Pride and Prejudice", Copies = 0 }
            };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(books);
            
            // Act
            var result = _service.GetAvailableBooks();
             
            // Assert
            Assert.Empty(result);
        }

    }
}
