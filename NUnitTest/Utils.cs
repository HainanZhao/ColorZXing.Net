using System.IO;
using NUnit.Framework;

namespace NUnitTest
{
    public class TestUtils
    {
        public static readonly string TextLong =
            @"JfHIur6hePTqT7E3YU7jxHFpTbDYrW9AMQGbaEJyjBGqlylshDhrdPqqgOPS6rYH1AEauJWCdD0OK4OU4bOu3wjihKILKN9UkzXiGeQQJRdBnMVtOtSDqmD9
            14SC9nOMPP7Hai4jaQKqZAw9JZbDEPm36WDaWtPIQik907A8YVww8LYYZ8TNUMHMCVCFjtSXnkZUeBVfIdwj0CbiqINBhWNsUHqIzoDTnXKe1KgppRj8GaZd
            K9s5S8J3gWyN6mnN5j470lKLlL2OV0qylEzbTIq3t4y2PzLZ4Bm9aUlmuxQLg0bJBZA7r38gdjM6eE9VvbZvmtNT9vbm18Lwh9pLstDFPxQdOVLMXgjRawQL
            ruxNetUfPfEKmahLyuQiSwmGTf3wUzcnwPE8nimgeWT80RJdyxhqKdiOnjYjLfTRTnQ7C0BEoliDFHWojff63qELo513MQrERtq1JkhQHLsgEcCPqbXoF0g0
            Qm4i3Ujo6yt6HbjbpjZqYPBuYpPrOitd37z0OG5xKmV1TsrIosHdVJ4bVgnBjdiiIZDMUyPuhrqI5IiARePtY9U6Vu5agtvBdokXpUXSMEEoF148h9Iqfeu0
            GxhLE357Ydo7X6FuWPgQFfLFLemszNhi30QxdzD1g8TwbsY54ZQJzYrNNfEqwKautYjPCUDfghu8Gdbo8eoTFpohbYLdjdt8ksgsxhP1bGt13I1PqXpNZ3yS
            odYyCmzAW3J1dgKkpIpRzx9YQ0mpgNj1ef7hjVCC5AfWc2fpQUX1JTqqDsZ7QFVwDN5iGhXi53uvyUPbPb0pn73QnCayVreuuBmT4Mt8DMvSiHvH5Yrmw7eM
            5PFeTIuAzdRuBVwnU1gVMBqkv0zBFsXuibSlBRfuy3YRztrmQEE0hinJWTKr96TZach0YTt9909Zn3GoQEZxBU5UeyJw2f2145gJauzpe1dRKeIVMcu72ggc";

        public static readonly string TextShort = "This is ColorZXing.Net, a lib that can generate colorful QR Codes.";

        public static string GetFilePath(string fileName)
        { 
            return Path.Combine(Path.GetDirectoryName(TestContext.CurrentContext.TestDirectory), fileName); 
        }        
    }
}
