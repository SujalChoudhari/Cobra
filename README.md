# Cobra

A simple, statically-typed, C-style programming language now built as an **interpreter** with .NET and ANTLR4.


## ⚡ Major Vision Update

Cobra is no longer just about writing a C-like interpreted language. The long-term goal is to make Cobra **a single language that can power entire websites and applications without needing separate HTML, CSS, and JavaScript.**

- Instead of switching between multiple languages and runtimes, developers will write everything in Cobra.
- A custom-built **Cobra Web Browser** will parse and render `.cb` files directly, giving Cobra full control over layout, styling, interactivity, and logic.
- This means Cobra isn’t just an interpreter—it’s a foundation for a self-contained **programming + rendering ecosystem**.

The project started as a native-code LLVM compiler, then pivoted into an extensible .NET interpreter, and is now moving toward integrating a **runtime + frontend rendering engine**, designed from scratch.

## About The Project

Cobra is an educational project for learning about interpreters and language design. It parses `.cb` files using an ANTLR-generated parser, then evaluates code directly with an interpreter written in C#.

The language continues to be C-like, but now emphasizes rapid turnaround and extensibility through interpretation.

### Built With

*   [.NET 9](https://dotnet.microsoft.com/en-us/download)
*   [ANTLR4](https://www.antlr.org/) for parsing
*   [CommandLineParser](https://github.com/commandlineparser/commandline) for CLI handling

## 🚧 Feature Checklist
### Cobra Interpreter Checklist

- [x] Define grammar in ANTLR
- [x] Set up project foundation and interpreter main loop
- [x] Create lexical analyzer using the generated lexer
- [x] Implement parser integration (parse input to syntax tree)
- [x] Build interpreter environment (scopes, variables, type system)
- [x] Implement statement execution:
    - [x] Variable declarations (`var`, `const`)
    - [x] Assignment and expressions
    - [x] Control flow (`if`, `while`, `for`)
    - [x] Function declarations and calls
    - [x] Functions in dicts and arrays
    - [x] Modules and imports
    - [ ] External functions and linking
    - [x] Namespaces
    - [ ] Error handling (try/catch/finally)
    - [x] Jump statements (`return`, `break`, `continue`)
    - [ ] `do-while`, `foreach`, `switch` (remaining control flow)
- [x] Array and dictionary literals
- [x] Data type system (primitive types)
- [x] Expression evaluation (precedence, operators)
- [x] Built-in functions and standard library (e.g., `print`)
- [ ] Runtime error reporting and diagnostics (basic implementation exists)
- [ ] Testing and sample programs
- [ ] Documentation and usage guide

### Cobra Web Rendering Engine (Planned)

- [ ] Define Cobra rendering grammar extensions (layout, elements, UI controls)
- [ ] Implement a rendering engine in C# (browser-like environment)
- [ ] Integrate event handling (click, hover, input)
- [ ] Build a DOM-like tree but powered by Cobra values/types
- [ ] Create a runtime UI library (buttons, text, images)
- [ ] Support interactive sites fully in Cobra

## 🚀 Why This Matters

The ultimate goal is to allow developers to:

- Launch a `.cb` file directly in the Cobra Browser, and see it render just like a webpage.
- Write interfaces, styling, and logic *in one unified language*.
- Eliminate the overhead of managing multiple web tech stacks.

Think of Cobra as a **language + runtime + browser** combined into one.


### 🐍 Clone the Project

1. Open a terminal.
2. Run:
    ```    git clone https://github.com/SujalChoudhari/Cobra.git
    cd Cobra
    ```

### 🤝 Contributor Workflow

- The project has a functional core interpreter! The foundation is solid.
- New feature branches and PRs are welcome for:
    - Implementing remaining control flow (`switch`, `foreach`)
    - Building the module/import system
    - Adding error handling (`try-catch`)
    - Creating more built-in functions

#### Suggested Steps:

1. **Create your own branch:**
    ```
    git checkout -b <feature-or-bugfix-name>
    ```
2. **Start work:**
   Add new interpreter code, tests, or documentation.
3. **Commit changes:**
    ```
    git add .
    git commit -m "Short but clear summary of your changes"
    ```
4. **Push your branch:**
    ```
    git push origin <your-branch-name>
    ```
5. **Open a Pull Request on GitHub.**
    - Briefly describe what you implemented or fixed.

---

**Tip:**
Check out the `Frontend/Cobra.g4` file to see the remaining grammar rules that need to be implemented in the interpreter!