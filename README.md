# JSONPath

[![Build Status][win-build-badge]][win-builds]
[![Build Status][nix-build-badge]][nix-builds]
[![NuGet][nuget-badge]][nuget-pkg]
[![MyGet][myget-badge]][edge-pkgs]

This project is a C# implementation of JSONPath.

## JSONPath Expressions

> [Portions &copy; 2006 Stefan G&ouml;ssner](http://goessner.net/articles/JsonPath/#e2)

JSONPath expressions always refer to a JSON structure in the same way as XPath
expression are used in combination with an XML document. Since a JSON structure is
usually anonymous and doesn't necessarily have a *root member object* JSONPath
assumes the abstract name `$` assigned to the outer level object.

JSONPath expressions can use the dot-notation:

    $.store.book[0].title

or the bracket-notation:

    $['store']['book'][0]['title']

for input paths. Internal or output paths will always be converted to the more
general bracket-notation.

JSONPath allows the wildcard symbol `*` for member names and array indices. It
borrows the descendant operator `..` from [E4X][e4x] and the [array slice
syntax][es4-slice] proposal `[start:end:step]` from ECMASCRIPT 4.

Expressions of the underlying scripting language (`<expr>`) can be used as an
alternative to explicit names or indices, as in:

    $.store.book[(@.length-1)].title

using the symbol `@` for the current object. Filter expressions are supported via
the syntax `?(<boolean expr>)`, as in:

    $.store.book[?(@.price < 10)].title

Below is a complete overview and a side-by-side comparison of the JSONPath
syntax elements with its XPath counterparts:

| XPath     | JSONPath           | Description                                                |
|:----------|:-------------------|:-----------------------------------------------------------|
| `/`       | `$`                | The root object/element                                    |
| `.`       | `@`                | The current object/element                                 |
| `/`       | `.` or `[]`        | Child operator                                             |
| `..`      | n/a                | Parent operator                                            |
| `//`      | `..`               | Recursive descent. JSONPath borrows this syntax from E4X.  |
| `*`       | `*`                | Wildcard. All objects/elements regardless their names.     |
| `@`       | n/a                | Attribute access. JSON structures don't have attributes.   |
| `[]`      | `[]`               | Subscript operator. XPath uses it to iterate over element collections and for [predicates][xpath-predicates]. In Javascript and JSON it is the native array operator. |
| `\|`      | `[,]`              | Union operator in XPath results in a combination of node sets. JSONPath allows alternate names or array indices as a set. |
| n/a       | `[start:end:step]` | Array slice operator borrowed from ES4.                    |
| `[]`      | `?()`              | Applies a filter (script) expression.                      |
| n/a       | `()`               | Script expression, using the underlying script engine.     |
| `()`      | n/a                | Grouping in XPath                                          |

  [e4x]: http://en.wikipedia.org/wiki/E4X
  [es4-slice]: http://developer.mozilla.org/es4/proposals/slice_syntax.html
  [xpath-predicates]: http://www.w3.org/TR/xpath#predicates

### Examples

> [Portions &copy; 2006 Stefan G&ouml;ssner](http://goessner.net/articles/JsonPath/#e3)

Let's practice JSONPath expressions by some more examples. We start with a
simple JSON structure built after an XML example representing a bookstore:

    { "store": {
        "book": [
          { "category": "reference",
            "author": "Nigel Rees",
            "title": "Sayings of the Century",
            "price": 8.95
          },
          { "category": "fiction",
            "author": "Evelyn Waugh",
            "title": "Sword of Honour",
            "price": 12.99
          },
          { "category": "fiction",
            "author": "Herman Melville",
            "title": "Moby Dick",
            "isbn": "0-553-21311-3",
            "price": 8.99
          },
          { "category": "fiction",
            "author": "J. R. R. Tolkien",
            "title": "The Lord of the Rings",
            "isbn": "0-395-19395-8",
            "price": 22.99
          }
        ],
        "bicycle": {
          "color": "red",
          "price": 19.95
        }
      }
    }

XPath                 | JSONPath                 | Result                                 | Notes
----------------------|--------------------------| ---------------------------------------|------
`/store/book/author`  | `$.store.book[*].author` | The authors of all books in the store  |
`//author`            | `$..author`              | All authors                            |
`/store/*`            | `$.store.*`              | All things in store, which are some books and a red bicycle |
`/store//price`       | `$.store..price`         | The price of everything in the store   |
`//book[3]`           | `$..book[2]`             | The third book                         |
`//book[last()]`      | `$..book[(@.length-1)]`<br>`$..book[-1:]`  | The last book in order |
`//book[position()<3]`| `$..book[0,1]`<br>`$..book[:2]`| The first two books              |
`//book/*[self::category\|self::author]` or `//book/(category,author)` in XPath 2.0 | `$..book[category,author]` | The categories and authors of all books |
`//book[isbn]`        | `$..book[?(@.isbn)]`     | Filter all books with `isbn` number    |
`//book[price<10]`    | `$..book[?(@.price<10)]` | Filter all books cheapier than 10      |
`//*[price>19]/..`    | `$..[?(@.price>19)]`     | Categories with things more expensive than 19 | Parent (caret) not present in original spec
`//*`                 | `$..*`                   | All elements in XML document; all members of JSON structure |
`/store/book/[position()!=1]` | `$.store.book[?(@path !== "$[\'store\'][\'book\'][0]")]` | All books besides that at the path pointing to the first | `@path` not present in original spec

See also:

- [RFC 9535: JSONPath: Query Expressions for JSON][rfc]

[win-build-badge]: https://img.shields.io/appveyor/ci/raboof/JSONPath/master.svg?label=windows
[win-builds]: https://ci.appveyor.com/project/raboof/JSONPath
[nix-build-badge]: https://img.shields.io/travis/atifaziz/JSONPath/master.svg?label=linux
[nix-builds]: https://travis-ci.org/atifaziz/JSONPath
[myget-badge]: https://img.shields.io/myget/raboof/vpre/JsonPathLib.svg?label=myget
[edge-pkgs]: https://www.myget.org/feed/raboof/package/nuget/JsonPathLib
[nuget-badge]: https://img.shields.io/nuget/v/JsonPathLib.svg
[nuget-pkg]: https://www.nuget.org/packages/JsonPathLib
[rfc]: https://www.rfc-editor.org/rfc/rfc9535.html
