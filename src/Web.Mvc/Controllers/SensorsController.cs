using Microsoft.AspNetCore.Mvc;
using Web.Mvc.Repositories;

namespace Web.Mvc.Controllers;

public class SensorsController : Controller
{
    private readonly ISensorRepository _sensorRepository;
    private readonly ILogger<SensorsController> _logger;

    public SensorsController(ISensorRepository sensorRepository, ILogger<SensorsController> logger)
    {
        _sensorRepository = sensorRepository;
        _logger = logger;
    }

    // GET: /Sensors
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Loaded sensors list view");
        var sensors = await _sensorRepository.GetAllSensorsAsync();
        return View(sensors);
    }

    // GET: /Sensors/Detail/{sensorId}
    public async Task<IActionResult> Detail(string sensorId)
    {
        _logger.LogInformation("Loading sensor detail for {SensorId}", sensorId);
        var sensor = await _sensorRepository.GetSensorDetailAsync(sensorId);

        if (sensor == null)
        {
            _logger.LogWarning("Sensor not found: {SensorId}", sensorId);
            return NotFound();
        }

        return View(sensor);
    }

    // GET: /Sensors/Map
    public async Task<IActionResult> Map()
    {
        _logger.LogInformation("Loaded sensors map view");
        var mapPoints = await _sensorRepository.GetMapPointsAsync();
        return View(mapPoints);
    }
}
