//using MediatR;

//namespace BarcodeDesignerDemo.Requests.Command
//{
//    public class CreateTemplateCommand : IRequest<bool>
//    {
//        public StudentDto StudentDto { get; set; }

//        public class CreateTemplateCommandHandler : IRequestHandler<CreateTemplateCommand, bool>
//        {
//            /// <summary>
//            /// IStudentDbContext
//            /// </summary>
//            public readonly IBarcodeDbContext _studentDbContext;

//            /// <summary>
//            /// Constructor
//            /// </summary>
//            public CreateTemplateCommandHandler(IBarcodeDbContext studentDbContext)
//            {
//                _studentDbContext = studentDbContext;
//            }
//            public async Task<bool> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
//            {
//                var studentDetails = request.StudentDto.AsStudentEntity();
//                var entityStudent = _studentDbContext.Students.Add(studentDetails);
//                await _studentDbContext.SaveChangesToDb();
//                return true;

//            }
//        }
//    }
//}
