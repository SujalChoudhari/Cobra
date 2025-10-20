
# Introduction to Cobra

Cobra is more than just a programming language; it's a dedicated ecosystem for creative coding. It was born from the idea that creating graphics, simulations, and interactive art should be expressive and accessible, not buried under layers of complex setup and boilerplate.

### The Vision: Unifying Logic and Visuals

In modern graphics development, a programmer often has to juggle multiple languages and APIs: a language like C++ or C# for application logic, a shading language like GLSL or HLSL for the GPU, and a complex API like OpenGL or Vulkan to connect the two. This creates a steep learning curve and a fragmented development process.

**Cobra's core vision is to eliminate this fragmentation.**

By integrating a powerful rendering engine directly into the language runtime, Cobra allows you to write your application logic, animations, and visual output in one seamless environment. The ultimate goal is to write everything—from physics simulations to pixel shaders—in pure Cobra.

### Core Principles

The development of Cobra is guided by a few key principles:

1.  **Simplicity and Readability:** The language should be easy to learn for programmers of all levels. The C-style syntax is intentionally familiar, and the built-in graphics commands are designed to be self-explanatory.
2.  **Low Barrier to Entry:** You shouldn't need to be a graphics expert to draw a shape on the screen. Cobra abstracts away the complexities of graphics drivers and APIs, letting you focus on the *what*, not the *how*.
3.  **Performance Where It Counts:** While Cobra is an interpreted language for rapid prototyping, it provides a direct FFI (`link`, `external`) to its high-performance C++ standard library. This ensures that critical operations like math, string manipulation, and eventually, rendering, run at native speed.
4.  **Excellent Developer Experience:** A language is only as good as its tools. Cobra is being built with a focus on providing clear, actionable error messages with full stack traces and source code context, making debugging an intuitive process.

### Who is Cobra For?

Cobra is designed for:

*   **Artists and Designers** who want to use code as a creative medium without getting bogged down in technical details.
*   **Students and Educators** looking for a simple, fun language to teach the fundamentals of programming and computer graphics.
*   **Game Developers** who need a tool for rapidly prototyping game mechanics and visual ideas.
*   **Data Scientists and Engineers** who want to create custom, real-time visualizations.

### Current Status

Cobra currently features a fully functional core interpreter. The language supports a rich set of features including variables, all major control flow statements, functions, object-oriented programming with classes, namespaces for code organization, and a native FFI.

The next major milestone is the implementation of the "Cobra Canvas," the integrated 2D/3D rendering library that will fulfill the language's core vision.