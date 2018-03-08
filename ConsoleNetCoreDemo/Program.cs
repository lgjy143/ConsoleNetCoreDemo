using Microsoft.Extensions.DependencyInjection;
using System;

namespace ConsoleNetCoreDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            //new CnBlogSubscribeTool().Build();

            new EventBusTool().Build();

            Console.ReadKey();
        }
    }
}
