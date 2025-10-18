# Cobra

A simple, object-oriented, C-style language designed for **creative coding and graphics**, built as an interpreter with .NET and ANTLR4.

## ⚡ Major Vision Update

Cobra is pivoting from its web-oriented goals to become a language focused on **graphics, visualization, and real-time rendering**. The new long-term goal is to make Cobra a single, easy-to-use language for both logic and visual output.

-   Instead of interfacing with complex graphics APIs, developers will write everything in Cobra.
-   The language will be tightly integrated with a **built-in 2D/3D rendering engine**, allowing developers to create visuals, animations, and simulations with minimal boilerplate.
-   This means Cobra isn’t just an interpreter—it’s a foundation for a self-contained **creative coding ecosystem**.

The project started as a native-code LLVM compiler, then became a general-purpose interpreter, and is now evolving into a specialized tool for artists, designers, and developers interested in graphics.

## About The Project

Cobra is an educational project for learning about interpreters and language design. It parses `.cb` files using an ANTLR-generated parser and evaluates them with a C# interpreter. The language supports procedural and object-oriented paradigms, making it flexible for scripting complex logic and structuring graphics-related code.

### Built With

*   [.NET 9](https://dotnet.microsoft.com/en-us/download)
*   [ANTLR4](https://www.antlr.org/) for parsing
*   [CommandLineParser](https://github.com/commandlineparser/commandline) for CLI handling


### Progress

* [x] Variable and constant declarations (`var`, `const`)
* [x] Primitive types (`int`, `float`, `bool`, `string`, `handle`)
* [x] Expression evaluation (arithmetic, logical, bitwise)
* [x] Function declarations and calls
* [x] Conditional logic (`if-else`)
* [x] Looping (`for`, `while`, `do-while`, `foreach`)
* [x] Branching (`switch-case`)
* [x] Jump statements (`return`, `break`, `continue`)
* [x] Object-oriented programming
    * [x] Class definitions
    * [x] Constructors and destructors
    * [x] Access modifiers (`public`, `private`)
    * [x] Static members
    * [x] Instance context (`this`)
* [x] Array literals
* [x] Dictionary literals
* [x] Namespaces
* [x] Module imports
* [x] Native FFI (`link`, `external`)
* [x] Functional core interpreter complete
* [x] Implement `try-catch-finally` error handling
* [ ] Expand standard library
    * [ ] Math library (vector, matrix, trig)
    * [ ] Utility libraries (random, string, etc.)
    * [ ] Exception classes
    * [ ] File I/O
* [ ] Expose high-level graphics API
* [ ] Implement “Cobra Canvas” for simple windowed drawing


## 🚀 Why This Matters

The ultimate goal is to **lower the barrier to entry for creative coding and graphics programming**. By integrating a powerful rendering engine directly into an easy-to-learn language, Cobra aims to:

-   **Simplify Graphics**: Abstract away the complexity of modern graphics APIs.
-   **Unify Logic and Rendering**: Allow developers to write game logic, simulations, and rendering code in one seamless language.
-   **Enable Rapid Prototyping**: Create a fun and interactive environment for quickly bringing visual ideas to life.

### 🐍 Clone the Project

1.  Open a terminal.
2.  Run:
    ```bash
    git clone https://github.com/SujalChoudhari/Cobra.git
    cd Cobra
    ```

### 🤝 Contributor Workflow

- The project has a functional core interpreter! The foundation is solid.
- New feature branches and PRs are welcome for:
    - Implementing `try-catch-finally`.
    - Expanding the standard library with math or utility functions.
    - Improving runtime error reporting.

#### Suggested Steps:

1.  **Create your own branch:**
    ```bash
    git checkout -b <feature-or-bugfix-name>
    ```
2.  **Start work:**
    Add new interpreter code, tests, or documentation.
3.  **Commit changes:**
    ```bash
    git add .
    git commit -m "Short but clear summary of your changes"
    ```
4.  **Push your branch:**
    ```bash
    git push origin <your-branch-name>
    ```
5.  **Open a Pull Request on GitHub.**
    -   Briefly describe what you implemented or fixed.

---

**Tip:**
Check out the `Frontend/Cobra.g4` file to see the remaining grammar rules that need to be implemented in the interpreter!