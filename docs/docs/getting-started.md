
# Getting Started

Welcome to Cobra! This guide will walk you through setting up your environment, building the interpreter, and running your first Cobra program.

### Prerequisites

Before you begin, you will need the **.NET 9 SDK**. You can download it from the official Microsoft website:

*   [Download .NET 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

You will also need `git` to clone the project repository.

### Step 1: Get the Cobra Source Code

Open your terminal or command prompt and clone the Cobra repository from GitHub:

```bash
git clone https://github.com/SujalChoudhari/Cobra.git
cd Cobra
```

This will download the entire project, including the interpreter source code and sample files.

### Step 2: Build the Interpreter

Cobra is a .NET project. To build the interpreter executable, run the following command from the root of the `Cobra` directory:

```bash
dotnet build -c Release
```
This command compiles the C# source code and places the final `Cobra.exe` (on Windows) or `Cobra` (on Linux/macOS) executable in the `bin/Release/net9.0/` directory.

### Step 3: Write Your First Program

Let's start with the classic "Hello, World!".

1.  Create a new file named `hello.cb` in the root of the `Cobra` project directory.
2.  Open `hello.cb` in your favorite text editor and add the following line:

```csharp
// hello.cb
print("Hello, World from Cobra!");
```
The `print()` function is a built-in that outputs text to the console. All statements in Cobra must end with a semicolon `;`.

### Step 4: Run Your Program

To run your script, you execute the Cobra interpreter and pass the path to your file as an argument.

**On Windows:**
```bash
.\bin\Release\net9.0\Cobra.exe hello.cb```

**On Linux or macOS:**
```bash
./bin/Release/net9.0/Cobra hello.cb
```

You should see the following output in your terminal:
```
Hello, World from Cobra!
```

### A More Detailed Example

Let's try something that shows off a few more features, like variables and loops. Create a file named `countdown.cb`:

```csharp
// countdown.cb

// This is a for loop, similar to C or Java.
for (int i = 10; i > 0; i--) {
    print("Countdown: " + i);
}

print("Liftoff!");
```

Run it the same way as before:
```bash
.\bin\Release\net9.0\Cobra.exe countdown.cb
```

### Next Steps

Congratulations! You've successfully built the Cobra interpreter and run your first scripts.

Now that you have the basics down, it's time to learn more about the language itself. Head over to our **[Language Basics](language-basics.md)** guide to learn about variables, data types, and control flow.