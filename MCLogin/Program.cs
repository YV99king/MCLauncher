﻿using System;

namespace MCLogin;

public static class Program
{
	public static void Main()
	{
        Console.Write("username: "); var username =  Console.ReadLine();
        Console.Write("password: "); var password = Console.ReadLine();
        _ = new Login(username, password);
    }
}
