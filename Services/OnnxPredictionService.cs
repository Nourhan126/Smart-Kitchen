using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SmartKitchen.API.Services;

public class OnnxPredictionService : IDisposable
{
    private readonly InferenceSession _session;

    public OnnxPredictionService()
    {
        var modelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Models",
            "warning_model.onnx"
        );

        _session = new InferenceSession(modelPath);
    }

    public int Predict(
        float gas,
        float flame,
        float temperature,
        float humidity)
    {
        var inputTensor = new DenseTensor<float>(
            new float[]
            {
                gas,
                flame,
                temperature,
                humidity
            },
            new[] { 1, 4 }
        );

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(
                "float_input",
                inputTensor
            )
        };

        using var results = _session.Run(inputs);

        var prediction = results
            .First(r => r.Name == "output_label")
            .AsEnumerable<long>()
            .First();

        return (int)prediction;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}