using System.IO;

namespace ET
{
    public class FileLogger: ILog
    {
        private readonly StreamWriter _stream;

        public FileLogger(string path)
        {
            _stream = new StreamWriter(File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
            _stream.AutoFlush = true;
        }

        public void Trace(string message)
        {
            _stream.WriteLine(message);
            _stream.Flush();
        }

        public void Warning(string message)
        {
            _stream.WriteLine(message);
            _stream.Flush();
        }

        public void Info(string message)
        {
            _stream.WriteLine(message);
            _stream.Flush();
        }

        public void Debug(string message)
        {
            _stream.WriteLine(message);
            _stream.Flush();
        }

        public void Error(string message)
        {
            _stream.WriteLine(message);
            _stream.Flush();
        }

        public void Trace(string message, params object[] args)
        {
            _stream.WriteLine(message, args);
            _stream.Flush();
        }

        public void Warning(string message, params object[] args)
        {
            _stream.WriteLine(message, args);
            _stream.Flush();
        }

        public void Info(string message, params object[] args)
        {
            _stream.WriteLine(message, args);
            _stream.Flush();
        }

        public void Debug(string message, params object[] args)
        {
            _stream.WriteLine(message, args);
            _stream.Flush();
        }

        public void Error(string message, params object[] args)
        {
            _stream.WriteLine(message, args);
            _stream.Flush();
        }

        public void Fatal(string message, params object[] args)
        {
            _stream.WriteLine(message, args);
            _stream.Flush();
        }
    }
}