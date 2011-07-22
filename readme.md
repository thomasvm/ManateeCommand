
ManateeCommand
==============
ManateeCommand is a different approach to idea that was originally pitched and implemented by Rob Conery as [Manatee](https://github.com/robconery/Manatee), a _Manatee is a single-file, drop-in database migrator for ASP.NET/WebMatrix using .NET 4.0_.  While this implementation can be very useful in WebMatrix projects, ManateeCommand takes another direction by providing the same functionality as _command-line database migrator_.  This project started from his code.

How Do You Use It?
------------------
This section mostly comes from the original project
### Command line
*To be extended*, but you can ask the exe for its options by doing

    mc.exe --help

### Migration files
It works with JSON and before you think "puke" - JSON can be used as a bit of a DSL/script that reads rather nicely. If you can write an anonymous object in C#, you can write JSON. Get over it.

The simplest thing to do is call your DDL directly:

    {
        up: "CREATE TABLE Orders (ID {pk}, OrderNumber {string} NOT NULL, SubTotal {money})",
        down: "DROP TABLE Orders"
    }
	
Some people like SQL. I like SQL. And then some don't. That's OK - if you're in the latter camp I'll help you in a second. In this example I've just told SQL to create a table for an "up" operation - a "version up" if you will. In there I have some replacement strings that will get replaced with the default values I like. `string` is `varchar(255)`, `money` is `decimal(8,2)`, `pk` is `int NOT NULL IDENTITY(1,1) PRIMARY KEY`. You get the idea.

Note that this JSON object describes what to do on an "up" call, and it's exact opposite is run on the "down" call. You don't have to do this if you use the JSON bits below. If you go straight SQL, you do.

Each of the JSON examples below is part of a single file. Ideally you will do a single operation per file (it's what the Rails guys do and it's made sense to me over time). The files are sorted in a SortedDictionary by key - and that key is a file name so it has to be something sortable. One thing you can do is a format like `YEAR_MONTH_DAY_TIME_description.js`. So this might, in reality, look like `2011_04_21_1352_create_products.js`. It's wordy, but it provides some nice meta data.

The next simplest thing to do is to specify a few things with some more structure:

    {
        up: {
            create_table: {
                name: "categories",
                timestamps: true,
                columns: [
                    { name: "title", type: "string" },
                    { name: "description", type: "text" }
                ]
            }
        }
    }

In this example I'm using structured JSON - setting the table name and the columns (which need to be an array). JSON can be tricky for some people - but it's just the same as C# anonymous object declaration and after you do it once or twice you'll dig it.

The datatypes used here are the same shorthand as the SQL call above - string will be converted the same way (as will money, text, boolean, and so on). Also - a bit of sweetness thrown in - if you want to have "audit" columns you can by setting `timestamps` to true. This will drop in two columns: "CreatedOn" and "UpdatedOn" that you should update when saving your data.

Finally - notice that there's no primary key defined? I meant to  - and sometimes we forget these things. I won't let you  - if you forget a PK it will automatically added for you (and called "Id").
	
Note that there is no "down" declared here. Create table has a pretty understandable reverse - "DROP TABLE" and we can infer that from this code. If you want to specify a "down" - go for it - that would look like this:

    {
        up: {
            create_table: {
                name: "products",
                columns: [
                    { name: "title", type: "string" },
                    { name: "description", type: "string" },
                    { name: "price", type: "money" }
                ]
            }
        },
        down: {
            drop_table: "products"
        }
    }

Once you're up and running with your new tables, you'll likely want to change them. You can do that by adding a column:

    {
        up: {
            add_column: {
                table: "categories",
                columns: [
                    { name: "slug", type: "string" }
                ]
            }
        },
        down: {
            remove_column: {
                table: "categories",
                name: "slug"
            }
        }
    }
	
Note the reverse here uses "remove_column". If you use Rails you might recognize these names :). You can also modify an existing column if you like:

    {
        up: {
            change_column: {
                table: "categories",
                columns: [
                    { name: "slug", type: "boolean" }
                ]
            }
        },
        down: {
            change_column: {
                table: "categories",
                columns: [
                    { name: "slug", type: "string" }
                ]
            }
        }
    }

To add indexes to your tables just specify the tables and the columns you want included in the index. The name will be generated for you by convention.
The down definition is optional as well.  It will be handled if you don't include it.

    {
        'up':{
            add_index:{
                table_name:"categories",
                columns:[
                    "title",
                    "slug"
                 ]
            }
        },
        'down':{
            remove_index:{
                table_name:"categories",
                columns:[
                    "title",
                    "slug"
                 ]
            }
        }
    }

ManateeCommand additions
------------------------
### Foreign keys
To add foreign keys to you tables, you can use foreign\_key as the creation command, and drop\_constraint as the down definition.  It will be handled automatically if you don't include it.

    {
        up: {
            foreign_key: {
                name: "fk_categories",
                from: {
                    table: "subcategories",
                    columns: [ "categoryid" ]
                },
                to: {
                    table: "categories",
                    columns: ["categoryid"]
                }
            }
        },
        down {
            drop_constraint: {
                table: "subcategories",
                name: "fk_categories"
            }
        }
    }

### Defaults
Columns can define default value constraints.

       up: 
       {
           create_table: {
                name: "products",
                columns: [
                    { name: "title", type: "string" },
                    { name: "description", type: "string"
                      default: {
                        name: "DF_description",
                        value: "'<description>'"
                      } },
                ]
            }
       }

### Multiple up and down commands
If you declare the up or the down operation as an json array, then you all those commands will be executed in sequence.  This is useful when you want to keep some operations together, or to minimize the amount of migration files needed when setting up an initial database model. Remember to that this is not encouraged (see above) and looks less nice.

    {
        up: [
            {
                create_table: {
                     name: "products",
                     columns: [
                         { name: "title", type: "string" },
                         { name: "description", type: "string" },
                         { name: "price", type: "money" }
                     ]
                 }
            },
            {  execute: "INSERT INTO products(title, description, price) VALUES('Test', 'description', 20.0)" }
        ],
        down: [
            { execute: "DELETE FROM products where title = 'Test'" },
            { drop_table: "products" }
        ]
    }

### Execute\_file
The sql that needs to be executed can also be specified in a file outside the migration definition. This is useful when the sql to execute is becoming to big to simply include in the migration. It also has the benefit that one can write the sql in his preferred editor. ManateeCommand will look for the files in the same folder as the migrations

    {
        up: {
            execute_file: "20110622_populate_pages.sql"
        },
        down: {
            execute: "DELETE FROM pages WHERE ModifiedBy = 'script'"
        }
    }

### Execute arrays
Json doesn't support multi-line strings.  Sometimes a sql statement can easily fit on a single line, sometimes  a sql statement is so big that you'd rather have in a separate file. But there's also a set of statements that are just a couple of lines, so creating a separate file involves too much friction.  In this scenario you can use execute _arrays_.

    {
        up: {
            execute: [
                "DELETE FROM pages ",
                "WHERE ModifiedBy = 'script'"
            ]
        }
    }

### Deriving from an existing data model
This is still very much _WIP_, but the intention is that one can ask ManateeCommand to create a set of migration files when provided an existing database.  This to speed up development against already established data models.

