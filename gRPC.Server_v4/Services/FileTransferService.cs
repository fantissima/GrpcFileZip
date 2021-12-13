using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace gRPC.Server_v4
{
    public class FileTransferService : FileTransfer.FileTransferBase
    {

        private readonly ILogger<FileTransferService> _logger;
        public FileTransferService(ILogger<FileTransferService> logger)
        {
            _logger = logger;
        }


        public override async Task Upload(IAsyncStreamReader<FileUploadRequest> request,
    IServerStreamWriter<FileUploadReply> reply, ServerCallContext context)
        {
            List<byte> content = new List<byte>();

            await foreach (FileUploadRequest message in request.ReadAllAsync())
            {
                content.AddRange(message.FileContent.ToByteArray());

                if (content.Count == message.FileSize)
                {
                    string fileName = Path.GetFileName(message.FilePath);
                    byte[] file = content.ToArray();
                    file = ZipFile(file, fileName);
                    await StreamFileToClient(file, message, reply);
                }
            }
        }


        private static async Task StreamFileToClient(byte[] file, FileUploadRequest message, IServerStreamWriter<FileUploadReply> reply)
        {
            FileUploadReply fileUploadReply = new FileUploadReply();
            fileUploadReply.FilePath = message.FilePath;
            fileUploadReply.FileSize = file.Length;
            int maxTransferSize = 2 << 20;
            int fileParts = file.Length / maxTransferSize;
            int fileLeftOver = file.Length % maxTransferSize;

            if (fileLeftOver != 0)
                fileParts++;

            byte[] sendContent = new byte[maxTransferSize];

            for (int index = 0; index < fileParts; index++)
            {
                int startPos = index * maxTransferSize;
                // transfer leftover part
                if (index == (fileParts - 1))
                {
                    sendContent = new byte[fileLeftOver];
                    Array.Copy(file, startPos, sendContent, 0, fileLeftOver);
                }
                else
                {
                    Array.Copy(file, startPos, sendContent, 0, maxTransferSize);
                }

                fileUploadReply.FileContent = Google.Protobuf.ByteString.CopyFrom(sendContent);
                await reply.WriteAsync(fileUploadReply);
            }
        }



        private byte[] ZipFile(byte[] file, string fileName)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    ZipArchiveEntry demoFile = zipArchive.CreateEntry(fileName);
                    Stream entryStream = demoFile.Open();
                    entryStream.Write(file, 0, file.Length);
                }

                return file = stream.ToArray();
            }
        }
    }
}