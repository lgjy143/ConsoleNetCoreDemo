using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleNetCoreDemo
{
    public class ReflectionTool
    {

        public class MyClass
        {
            public static void MyMethodType<T>()
            {
                Console.WriteLine(typeof(T).ToString());
            }
        }

        public void MyMethod<T>()
        {
            Console.WriteLine(typeof(T).ToString());
        }
    }
}
