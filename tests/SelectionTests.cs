#region Copyright (c) 2007 Atif Aziz. All rights reserved.
//
// C# implementation of JSONPath[1]
// [1] http://goessner.net/articles/JsonPath/
//
// The MIT License
//
// Copyright (c) 2007 Atif Aziz . All rights reserved.
// Portions Copyright (c) 2007 Stefan Goessner (goessner.net)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
#endregion

namespace JsonPath.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Jint;
    using Xunit;
    using Object = System.Collections.Generic.Dictionary<string, object>;

    public class SelectionTests
    {
        static readonly Object[] Books =
        {
            new Object
            {
                ["category"] = "reference",
                ["author"] = "Nigel Rees",
                ["title"] = "Sayings of the Century",
                ["price"] = 8.95m
            },
            new Object
            {
                ["category"] = "fiction",
                ["author"] = "Evelyn Waugh",
                ["title"] = "Sword of Honour",
                ["price"] = 12.99m
            },
            new Object
            {
                ["category"] = "fiction",
                ["author"] = "Herman Melville",
                ["title"] = "Moby Dick",
                ["isbn"] = "0-553-21311-3",
                ["price"] = 8.99m
            },
            new Object
            {
                ["category"] = "fiction",
                ["author"] = "J. R. R. Tolkien",
                ["title"] = "The Lord of the Rings",
                ["isbn"] = "0-395-19395-8",
                ["price"] = 22.99m
            }
        };

        static readonly Object Bicycle = new Object
        {
            ["color"] = "red",
            ["price"] = 19.95m
        };

        static readonly Object Store = new Object
        {
            ["book"] = Books,
            ["bicycle"] = Bicycle,
        };

        static readonly Object Data = new Object
        {
            ["store"] = Store
        };

        static IEnumerable<(T Item, string Path)> SelectNodes<T>(string expr, Func<string, object, object> eval = null) =>
            new JsonPathContext
            {
                ScriptEvaluator =
                    eval != null
                    ? new Func<string, object, string, object>((script, value, context) => eval(script, value))
                    : null
            }.SelectNodes(Data, expr, (m, p) => ((T) m, p));

        [Theory, InlineData("$.store.book[*].author")]
        public void TheAuthorsOfAllBooksInTheStore(string expr)
        {
            var matches = SelectNodes<string>(expr).Select(e => new { e.Item, e.Path });
            var expected = new[]
            {
                new { Item = (string) Books[0]["author"], Path = "$['store']['book'][0]['author']" },
                new { Item = (string) Books[1]["author"], Path = "$['store']['book'][1]['author']" },
                new { Item = (string) Books[2]["author"], Path = "$['store']['book'][2]['author']" },
                new { Item = (string) Books[3]["author"], Path = "$['store']['book'][3]['author']" },
            };
            Assert.Equal(expected, matches);
        }

        [Theory, InlineData("$..author")]
        public void AllAuthors(string expr)
        {
            var matches = SelectNodes<string>(expr).Select(e => new { e.Item, e.Path });
            var expected = new[]
            {
                new { Item = (string) Books[0]["author"], Path = "$['store']['book'][0]['author']" },
                new { Item = (string) Books[1]["author"], Path = "$['store']['book'][1]['author']" },
                new { Item = (string) Books[2]["author"], Path = "$['store']['book'][2]['author']" },
                new { Item = (string) Books[3]["author"], Path = "$['store']['book'][3]['author']" },
            };
            Assert.Equal(expected, matches);
        }

        [Theory, InlineData("$.store.*")]
        public void AllThingsInStoreWhichAreSomeBooksAndOneRedBicycle(string expr)
        {
            var matches = SelectNodes<object>(expr).Select(e => new { e.Item, e.Path });
            var expected = new[]
            {
                new { Item = (object) Books  , Path = "$['store']['book']" },
                new { Item = (object) Bicycle, Path = "$['store']['bicycle']" },
            };
            Assert.Equal(expected, matches);
        }

        [Theory, InlineData("$.store..price")]
        public void ThePriceOfEverythingInTheStore(string expr)
        {
            var matches = SelectNodes<decimal>(expr).Select(e => new { e.Item, e.Path });
            var expected = new[]
            {
                new { Item = (decimal) Books[0]["price"], Path = "$['store']['book'][0]['price']" },
                new { Item = (decimal) Books[1]["price"], Path = "$['store']['book'][1]['price']" },
                new { Item = (decimal) Books[2]["price"], Path = "$['store']['book'][2]['price']" },
                new { Item = (decimal) Books[3]["price"], Path = "$['store']['book'][3]['price']" },
                new { Item = (decimal) Bicycle["price"] , Path = "$['store']['bicycle']['price']" },
            };
            Assert.Equal(expected, matches);
        }

        [Theory, InlineData("$..book[2]")]
        public void TheThirdBook(string expr)
        {
            var match = SelectNodes<Object>(expr).Select(e => new { e.Item, e.Path }).Single();
            Assert.Equal(Books[2], match.Item);
            Assert.Equal("$['store']['book'][2]", match.Path);
        }

        [Theory, InlineData("$..book[-1:]")]
        public void TheLastBookInOrder(string expr)
        {
            var match = SelectNodes<Object>(expr).Select(e => new { e.Item, e.Path }).Single();
            Assert.Equal(Books.Last(), match.Item);
            Assert.Equal("$['store']['book'][3]", match.Path);
        }

        [Theory]
        [InlineData("$..book[:2]")]
        [InlineData("$..book[0,1]")]
        public void TheFirstTwoBooks(string expr)
        {
            var matches = SelectNodes<Object>(expr).Select(e => new { e.Item, e.Path });
            var expected = new[]
            {
                new { Item = Books[0], Path = "$['store']['book'][0]" },
                new { Item = Books[1], Path = "$['store']['book'][1]" },

            };
            Assert.Equal(expected, matches);
        }

        [Theory]
        [InlineData("$..book[category,author]",
                    Skip = "Broken as in the original jsonpath.js 0.8.5")]
        public void TheCategoriesAndAuthorsOfAllBooks(string expr)
        {
            var matches = SelectNodes<string>(expr).Select(e => new { e.Item, e.Path });
            var expected = new[]
            {
                new { Item = (string) Books[0]["category"], Path = "$['store']['book'][0]['category']" },
                new { Item = (string) Books[0]["author"  ], Path = "$['store']['book'][0]['author']"   },
                new { Item = (string) Books[1]["category"], Path = "$['store']['book'][1]['category']" },
                new { Item = (string) Books[1]["author"  ], Path = "$['store']['book'][1]['author']"   },
                new { Item = (string) Books[2]["category"], Path = "$['store']['book'][2]['category']" },
                new { Item = (string) Books[2]["author"  ], Path = "$['store']['book'][2]['author']"   },
                new { Item = (string) Books[3]["category"], Path = "$['store']['book'][3]['category']" },
                new { Item = (string) Books[3]["author"  ], Path = "$['store']['book'][3]['author']"   },

            };
            Assert.Equal(expected, matches);
        }

        [Theory, InlineData("$..book[?(@.isbn)]")]
        public void FilterAllBooksWithIsbnNumber(string expr)
        {
            var matches =
                SelectNodes<Object>(expr,
                                    (_, o) => o is Object book
                                           && book.TryGetValue("isbn", out var isbn)
                                           && !string.IsNullOrEmpty(isbn as string));
            var expected = new[]
            {
                new { Item = Books[2], Path = "$['store']['book'][2]" },
                new { Item = Books[3], Path = "$['store']['book'][3]" },
            };
            Assert.Equal(expected, matches.Select(e => new { e.Item, e.Path }));
        }

        [Theory, InlineData("$..book[?(@.price<10)]")]
        public void FilterAllBooksCheaperThan10(string expr)
        {
            var matches =
                SelectNodes<Object>(expr,
                                    (_, o) => o is Object obj
                                           && obj.TryGetValue("price", out var price)
                                           && price is decimal n
                                           && n < 10);
            var expected = new[]
            {
                new { Item = Books[0], Path = "$['store']['book'][0]" },
                new { Item = Books[2], Path = "$['store']['book'][2]" },
            };
            Assert.Equal(expected, matches.Select(e => new { e.Item, e.Path }));
        }

        [Theory, InlineData("$..*")]
        public void AllMembersOfJsonStructure(string expr)
        {
            var matches = SelectNodes<object>(expr).Select(e => new { e.Item, e.Path });
            var expected = new[]
            {
                new { Item = (object) Store      , Path = "$['store']"                        },
                new { Item = (object) Books      , Path = "$['store']['book']"                },
                new { Item = (object) Bicycle    , Path = "$['store']['bicycle']"             },
                new { Item = (object) Books[0]   , Path = "$['store']['book'][0]"             },
                new { Item = (object) Books[1]   , Path = "$['store']['book'][1]"             },
                new { Item = (object) Books[2]   , Path = "$['store']['book'][2]"             },
                new { Item = (object) Books[3]   , Path = "$['store']['book'][3]"             },
                new { Item = Books[0]["category"], Path = "$['store']['book'][0]['category']" },
                new { Item = Books[0]["author"]  , Path = "$['store']['book'][0]['author']"   },
                new { Item = Books[0]["title"]   , Path = "$['store']['book'][0]['title']"    },
                new { Item = Books[0]["price"]   , Path = "$['store']['book'][0]['price']"    },
                new { Item = Books[1]["category"], Path = "$['store']['book'][1]['category']" },
                new { Item = Books[1]["author"]  , Path = "$['store']['book'][1]['author']"   },
                new { Item = Books[1]["title"]   , Path = "$['store']['book'][1]['title']"    },
                new { Item = Books[1]["price"]   , Path = "$['store']['book'][1]['price']"    },
                new { Item = Books[2]["category"], Path = "$['store']['book'][2]['category']" },
                new { Item = Books[2]["author"]  , Path = "$['store']['book'][2]['author']"   },
                new { Item = Books[2]["title"]   , Path = "$['store']['book'][2]['title']"    },
                new { Item = Books[2]["isbn"]    , Path = "$['store']['book'][2]['isbn']"     },
                new { Item = Books[2]["price"]   , Path = "$['store']['book'][2]['price']"    },
                new { Item = Books[3]["category"], Path = "$['store']['book'][3]['category']" },
                new { Item = Books[3]["author"]  , Path = "$['store']['book'][3]['author']"   },
                new { Item = Books[3]["title"]   , Path = "$['store']['book'][3]['title']"    },
                new { Item = Books[3]["isbn"]    , Path = "$['store']['book'][3]['isbn']"     },
                new { Item = Books[3]["price"]   , Path = "$['store']['book'][3]['price']"    },
                new { Item = Bicycle["color"]    , Path = "$['store']['bicycle']['color']"    },
                new { Item = Bicycle["price"]    , Path = "$['store']['bicycle']['price']"    },
            };
            Assert.Equal(expected, matches);
        }

        [Theory, InlineData("$.store.book[?(@path !== \"$[\'store\'][\'book\'][0]\")]")]
        public void AllBooksBesidesThatAtThePathPointingToTheFirst(string expr)
        {
            var matches =
                from e in SelectNodes<Object>(expr, delegate { return true; })
                where e.Path != "$['store']['book'][0]"
                select new { e.Item, e.Path };

            var expected = new[]
            {
                new { Item = Books[1], Path = "$['store']['book'][1]" },
                new { Item = Books[2], Path = "$['store']['book'][2]" },
                new { Item = Books[3], Path = "$['store']['book'][3]" },
            };

            Assert.Equal(expected, matches);
        }

        [Theory, InlineData("$..book[?(@.price == 8.99 && @.category == 'fiction')]")]
        public void FilterAllBooksUsingLogicalAndInScript(string expr)
        {
            var engine = new Engine();

            object Eval(string script, object value) =>
                engine.SetValue("$$", value)
                      .Execute(script.Replace("@", "$$"))
                      .GetCompletionValue()
                      .ToObject();

            var matches = SelectNodes<Object>(expr, Eval);

            Assert.Equal(new { Item = Books[2], Path = "$['store']['book'][2]" },
                         matches.Select(e => new { e.Item, e.Path }).Single());
        }
    }
}
