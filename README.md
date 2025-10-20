# Cobra

**Cobra** is a **C-style, object-oriented programming language** built for **creative coding and real-time graphics**.
Developed as a **C# interpreter** using **ANTLR4** and the **visitor pattern**, Cobra aims to unify logic, visualization, and interactivity in one language.

---

## Technical Highlights

* **Parser:** Implemented using **ANTLR4**, generating lexer, parser, and visitor classes from the Cobra grammar (`Cobra.g4`).
* **Architecture:**

    * Follows the **Visitor pattern** for AST traversal and evaluation.
    * Modularized into `Frontend`, `Interpreter`, and `Environment` layers for clear separation of parsing, execution, and runtime context.
* **Interpreter Core:** Dynamically evaluates parsed AST nodes, manages symbol tables, scopes, and runtime type information.
* **Runtime Environment:**

    * Object-oriented runtime with user-defined classes, enums, and functions.
    * Exception handling, stack traces, and native interop (`external`, `link`).
* **Extensibility:** Designed for easy addition of new modules, built-in functions, and future **Cobra Canvas** (2D/3D graphics engine).
* **Goal:** Build a self-contained ecosystem for **creative coding**, similar in philosophy to Processing or p5.js, but with **strong typing and class-based OOP**.

---


## Vision

Cobra’s long-term vision is to become a **graphics-focused language** for developers, artists, and educators — a platform where logic, design, and rendering converge.

**Core goals:**

* Integrate a **native 2D/3D rendering engine**
* Remove external graphics dependencies
* Support **live visual coding**
* Deliver **strongly typed scripting** for creative work

---

## Language Overview

Cobra combines familiar syntax with modern features.

```csharp
import "exception";

enum State { Idle, Running, Paused = 10, Finished }

i8 health = 100;
f32 speed = 1.5;

void log(string msg) {
    print(__FILE__ + ":" + __LINE__ + " [" + __FUNC__ + "]: " + msg);
}

try {
    throw new System.Exception("Example error");
} catch (System.Exception e) {
    print("Caught: " + e.getMessage());
}
```

---

## Feature Matrix

| Category         | Description                                             | Status         |
| ---------------- | ------------------------------------------------------- | -------------- |
| Core Syntax      | Variables, constants, expressions                       | ✅ Complete     |
| Types            | Rich numeric and string system                          | ✅ Complete     |
| Control Flow     | `if`, `switch`, loops                                   | ✅ Complete     |
| OOP              | Classes, constructors, access modifiers, static members | ✅ Complete     |
| Enums            | Named constants with values                             | ✅ Complete     |
| Data Structures  | Arrays, dictionaries                                    | ✅ Complete     |
| Modules          | Namespaces, imports                                     | ✅ Complete     |
| Native Interop   | `link`, `external` for FFI                              | ✅ Complete     |
| Error Handling   | `try-catch-finally`, stack traces                       | ✅ Complete     |
| Metaprogramming  | `__FILE__`, `__LINE__`, `__FUNC__`                      | ✅ Complete     |
| Standard Library | Math, string, utility                                   | 🚧 In Progress |
| Graphics Engine  | “Cobra Canvas” visual system                            | 🚧 Planned     |

---

## Build and Run

**Requirements:**

* [.NET 9](https://dotnet.microsoft.com/en-us/download)
* [ANTLR4](https://www.antlr.org/) (for grammar regeneration)

**Steps:**

```bash
git clone https://github.com/SujalChoudhari/Cobra.git
cd Cobra
dotnet build
dotnet run -- <file.cb>
```

---

## Roadmap

* [ ] Expand Math and Utility libraries
* [ ] Add file I/O and runtime modules
* [ ] Implement Cobra Canvas for 2D/3D rendering
* [ ] Add time, animation, and concurrency modules
* [ ] Introduce bytecode or JIT compilation backend

---

## Contributing

Pull requests are welcome.
Good first contributions include:

* Adding standard library modules
* Enhancing the parser or visitor logic
* Extending runtime error diagnostics
* Prototyping the graphics subsystem

---

## Acknowledgment

Initial code generation assisted by **Gemini**, with architecture, structure, and logic authored and verified manually.
Cobra continues as an independent, open-source exploration in **language design and creative computing**.
