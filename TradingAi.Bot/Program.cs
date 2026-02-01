using System;
using TorchSharp;
using TradingAi.Core.Models; // LstmModel is now in the Core project
using static TorchSharp.torch;

// 1. Set up configuration (must match the training configuration!)
var device = torch.CPU; // CPU is sufficient for inference

Console.WriteLine("[Bot] Starting System...");

// 2. Create an empty shell for the model
// Important: Must be instantiated with the same class and size as during training
Console.WriteLine($"[Bot] Creating Model... (Input: {ModelDefinition.InputSize}, Hidden: {ModelDefinition.HiddenSize})");
var model = new LstmModel("Predictor", ModelDefinition.InputSize, ModelDefinition.HiddenSize);

// 3. Load the trained model file (.dat)
// The file path is relative to the execution directory.
// The .csproj file is configured to copy 'Models/UniversalModel.dat' to the output.
const string modelPath = "../../../Models/UniversalModel.dat";

try 
{
    if (!System.IO.File.Exists(modelPath))
    {
        throw new System.IO.FileNotFoundException($"Model file not found at '{System.IO.Path.GetFullPath(modelPath)}'. Ensure it is set to 'Copy to Output Directory'.");
    }
    model.load(modelPath); 
    model.to(device); // Place the model on the CPU (or GPU)
    model.eval();     // CRITICAL: Switch from 'training mode' to 'inference mode'
    Console.WriteLine("[Bot] Model Loaded Successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"[Error] Failed to load model: {ex.Message}");
    return;
}

// 4. Create dummy data for testing
// In a real scenario, this would be real-time data from a brokerage.
// Shape: [batch_size=1, sequence_length=30, num_features=4]
var dummyInput = torch.randn(1, 30, ModelDefinition.InputSize).to(device);

// 5. Execute prediction (Inference)
Console.WriteLine("[Bot] Predicting...");

using (torch.no_grad()) // Important: Declare that we are not training (improves performance and saves memory)
{
    var output = model.forward(dummyInput);
    
    // Extract the float value from the tensor
    float probability = output.item<float>();

    Console.WriteLine("-----------------------------");
    Console.WriteLine($"AI Prediction: {probability:F4}"); // Outputs a number between 0.0 and 1.0
    Console.WriteLine("-----------------------------");

    // 6. Example trading logic
    if (probability > 0.8)
    {
        Console.WriteLine("Decision: Strong signal! Placing a BUY order.");
    }
    else if (probability < 0.2)
    {
        Console.WriteLine("Decision: High risk of a drop! Placing a SELL order.");
    }
    else
    {
        Console.WriteLine("Decision: Wait and see.");
    }
}