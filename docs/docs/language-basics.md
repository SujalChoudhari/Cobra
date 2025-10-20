
# Language Basics

This guide covers the fundamental syntax and features of the Cobra language.

### Comments

Cobra supports two types of comments, just like C++ and C#:

```csharp
// This is a single-line comment.

/*
  This is a
  multi-line block comment.
*/
```

### Variables and Constants

Variables are declared with the `var` keyword and can be reassigned. Constants are declared with `const` and must be initialized upon declaration.

```csharp
// A variable of type 'int'
int score = 0;
score = 100; // This is valid.

// A constant of type 'float'
const float PI = 3.14;
// PI = 3.14159; // This would cause a compiler error.
```

### Data Types

Cobra has a rich set of primitive data types for handling numbers, text, and logic.

#### Integer Types
Cobra provides fixed-width integer types for precise control over data.

*   **Signed Integers:** `i8`, `i16`, `i32` (or `int`), `i64` (or `long`)
*   **Unsigned Integers:** `u8`, `u16`, `u32` (or `uint`), `u64` (or `ulong`)

```csharp
i8 temperature = -5;
u32 population = 4000000;
long stars_in_galaxy = 100000000000;
```

#### Floating-Point Types
For numbers with fractional components.

*   `f32` (or `float`): 32-bit single-precision float.
*   `f64` (or `double`): 64-bit double-precision float.

```csharp
float speed = 9.81;
double planck_constant = 6.62607015e-34;
```

#### Other Primitives
*   `bool`: Can be `true` or `false`.
*   `string`: A sequence of text characters, enclosed in double quotes.

```csharp
bool is_active = true;
string message = "Welcome to Cobra!";
```

### Operators

Cobra supports standard arithmetic, comparison, and logical operators.

*   **Arithmetic:** `+`, `-`, `*`, `/`, `%` (modulo)
*   **Comparison:** `==` (equals), `!=` (not equals), `<`, `>`, `<=`, `>=`
*   **Logical:** `&&` (and), `||` (or), `!` (not)

```csharp
int health = 100;
health = health - 25; // 75

bool is_alive = health > 0; // true
bool can_proceed = is_alive && has_key;
```

### Control Flow

#### If / Else
Execute code conditionally.

```csharp
if (score > 1000) {
    print("High score!");
} else if (score > 500) {
    print("Good job!");
} else {
    print("Keep trying!");
}
```

#### Loops
Cobra supports `for`, `foreach`, `while`, and `do-while` loops.

```csharp
// C-style for loop
for (int i = 0; i < 5; i++) {
    print(i);
}

// while loop
int countdown = 3;
while (countdown > 0) {
    print(countdown);
    countdown--;
}

// foreach loop (for lists and arrays)
int[] numbers =;
for (int num in numbers) {
    print("Number: " + num);
}
```

### Functions

Functions are blocks of reusable code. You must specify the return type and the types of all parameters.

```csharp
// A function that takes two integers and returns an integer.
int add(int a, int b) {
    return a + b;
}

// A function that does not return a value.
void greet(string name) {
    print("Hello, " + name);
}

// Calling the functions
int sum = add(5, 10);
greet("Alice");
```