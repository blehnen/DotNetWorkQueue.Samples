using System.Text;
using DotNetWorkQueue;
using DotNetWorkQueue.Logging;
using Microsoft.Extensions.Logging;

namespace SampleShared
{
    public class SimpleMessage
    {
        public string Message { get; set; }
        public int ProcessingTime { get; set; }

        public ErrorTypes Error { get; set; }
    }

    public enum ErrorTypes
    {
        None = 0,
        Error = 1,
        RetryableError = 2,
        RetryableErrorFail = 3
    }

    public class TestClass
    {
        public void RunMe(IWorkerNotification workNotification, string input1, int input2, SomeInput moreInput)
        {
            //a constant template with the values as arguments - the previous StringBuilder
            //produced a different template on every call, which defeats structured logging
            //and forces the string to be built even when the level is disabled
            workNotification.Log.LogInformation("{Input1} {Input2} {MoreInput}",
                input1, input2, moreInput.Message);
        }
    }

    public class SomeInput
    {
        public SomeInput()
        {
        }

        public SomeInput(string message)
        {
            Message = message;
        }

        public string Message { get; set; }
        public override string ToString()
        {
            return Message;
        }
    }
}
