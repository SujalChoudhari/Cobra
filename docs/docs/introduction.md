# Cobra: The Expressive Language for Creative Coding

**Cobra is a simple, C-style language designed from the ground up for graphics, real-time rendering, and creative expression.** It removes the boilerplate of complex graphics APIs, allowing you to focus on your art, simulations, and visual ideas.

Cobra unifies high-level logic and low-level rendering in a single, easy-to-learn language. It's a complete ecosystem for bringing your visual projects to life.

---

### Key Features

*   **Simple & Familiar Syntax:** If you know C, C++, C#, or JavaScript, you'll feel right at home with Cobra's clean, C-style syntax.
*   **Graphics-First Design:** The language is being built with a tightly integrated 2D/3D rendering engine in mind. The goal is to make drawing shapes, handling animations, and creating shaders feel like a natural part of the language.
*   **Object-Oriented:** Structure your complex projects cleanly with classes, namespaces, and modules.
*   **Native Performance:** With a C++ standard library and a Foreign Function Interface (FFI), Cobra allows you to call high-performance native code directly, ensuring your creative visions are never limited by speed.
*   **Modern Tooling:** Built on .NET 9 and ANTLR4, Cobra is engineered for a great developer experience with features like detailed error reporting and stack traces.

### A Glimpse of the Future

While the rendering engine is under development, Cobra is designed for code that feels intuitive and visual. Here’s a conceptual look at what drawing a simple animation will look like:

```csharp
// The future vision for Cobra's rendering API

void setup() {
    // Set up the canvas once at the start
    Canvas.create(800, 600, "Bouncing Ball");
}

float x = 400;
float y = 300;
float x_speed = 2.5;
float y_speed = 2.0;

void draw() {
    // This function will be called every frame
    Canvas.clear(Color.Black);

    // Update position
    x += x_speed;
    y += y_speed;

    // Handle bouncing off the walls
    if (x > 800 || x < 0) {
        x_speed *= -1;
    }
    if (y > 600 || y < 0) {
        y_speed *= -1;
    }

    // Draw the circle
    Canvas.drawCircle(x, y, 20, Color.Red);
}

```


