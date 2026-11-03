using System;
using System.Collections.Generic;
using System.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var numbers = new List<int> { 1, 2, 3 };
var query = numbers.Where(n => n % 2 == 0);
var snapshot = query.ToList();
numbers.Add(4);
Console.WriteLine(string.Join(",", query));
Console.WriteLine(string.Join(",", snapshot));
