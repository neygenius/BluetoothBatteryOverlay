using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Devices.Power;
using Windows.Storage;
using Windows.System.Power;

class Program
{
    static async Task Main(string[] args)

    {
        Console.WriteLine("Запуск сканирования Bluetooth HID-устройств с состоянием заряда...");
        await ScanBluetoothDevices();
        Console.WriteLine("\nСканирование завершено. Нажмите Enter для выхода...");
        Console.ReadLine();
    }

    static async Task ScanBluetoothDevices()
    {
        Console.WriteLine("Поиск Bluetooth HID-устройств в системе...\n");

        try
        {
            ushort usagePage = 0x0001; // Generic Desktop Controls
            ushort usageId = 0x0002;   // Mouse
            string selector = HidDevice.GetDeviceSelector(usagePage, usageId);
            var deviceInfos = await DeviceInformation.FindAllAsync(selector, new[] { "System.Devices.DeviceInstanceId" });

            if (deviceInfos.Count == 0)
            {
                Console.WriteLine("HID-устройства (мыши) не найдены.");
                return;
            }

            foreach (var deviceInfo in deviceInfos)
            {
                string deviceId = deviceInfo.Id;
                bool isBluetooth = Regex.IsMatch(deviceId, @"HID#\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}") && deviceId.Contains("&Col01");

                if (isBluetooth)
                {
                    Console.WriteLine("=== Беспроводное HID-устройство ===");
                    Console.WriteLine($"Имя: {deviceInfo.Name ?? "Неизвестное устройство"}");
                    Console.WriteLine($"ID: {deviceId}");
                    Console.WriteLine("Свойства устройства:");
                    foreach (var prop in deviceInfo.Properties)
                    {
                        Console.WriteLine($"  {prop.Key}: {prop.Value}");
                    }

                    string batteryStatus = await GetBatteryStatus(deviceInfo, deviceId);
                    Console.WriteLine($"Состояние заряда: {batteryStatus}");

                    try
                    {
                        var hidDevice = await HidDevice.FromIdAsync(deviceId, FileAccessMode.Read);
                        if (hidDevice != null)
                        {
                            Console.WriteLine($"Подключено: {deviceInfo.Properties.ContainsKey("System.Devices.Aep.IsConnected") && (bool)deviceInfo.Properties["System.Devices.Aep.IsConnected"]}");
                            Console.WriteLine($"VID: {hidDevice.VendorId}");
                            Console.WriteLine($"PID: {hidDevice.ProductId}");
                            Console.WriteLine($"Версия: {hidDevice.Version}");
                            // Попытка получить данные о заряде через HID (нужна спецификация)
                            // Это требует знания формата отчета от ATK
                            // byte[] report = new byte[hidDevice.GetFeatureReportLength()];
                            // await hidDevice.GetFeatureReportAsync(report);
                            // Console.WriteLine($"HID-отчет: {BitConverter.ToString(report)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при доступе к устройству: {ex.Message}");
                    }

                    Console.WriteLine("==================================");
                }
            }
            Console.WriteLine($"Всего найдено Bluetooth-устройств: {deviceInfos.Count(d => Regex.IsMatch(d.Id, @"HID#\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}") && d.Id.Contains("&Col01"))}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    static async Task<string> GetBatteryStatus(DeviceInformation deviceInfo, string deviceId)
    {
        try
        {
            string batterySelector = Battery.GetDeviceSelector();
            var batteryInfos = await DeviceInformation.FindAllAsync(batterySelector, new[] { "System.Devices.DeviceInstanceId" });

            Console.WriteLine($"Найдено устройств с батареей: {batteryInfos.Count}");
            if (batteryInfos.Count > 0)
            {
                Console.WriteLine("Устройства с батареей:");
                foreach (var batteryInfo in batteryInfos)
                {
                    Console.WriteLine($"  Имя: {batteryInfo.Name ?? "Неизвестно"}");
                    Console.WriteLine($"  ID: {batteryInfo.Id}");
                    foreach (var prop in batteryInfo.Properties)
                    {
                        Console.WriteLine($"    {prop.Key}: {prop.Value}");
                    }

                    var battery = await Battery.FromIdAsync(batteryInfo.Id);
                    if (battery != null)
                    {
                        var report = battery.GetReport();
                        if (report.Status == BatteryStatus.NotPresent)
                        {
                            return "Не определено (батарея отсутствует или не поддерживается)";
                        }
                        if (report.RemainingCapacityInMilliwattHours.HasValue && report.FullChargeCapacityInMilliwattHours.HasValue)
                        {
                            int chargeLevel = (int)((report.RemainingCapacityInMilliwattHours.Value * 100) / report.FullChargeCapacityInMilliwattHours.Value);
                            return $"{chargeLevel}%";
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"Попытка использования deviceId: {deviceId} для поиска батареи не удалась.");
                Console.WriteLine("Устройства с батареей не найдены. Возможно, драйверы устройства не поддерживают отчет о заряде через Battery API.");
                Console.WriteLine("Рекомендуется установить ATK V HUB с https://www.atk.store для просмотра статуса батареи.");
                Console.WriteLine("Также проверьте обновления драйверов Bluetooth или фирменное ПО от ATK.");
                Console.WriteLine("Заметка: Заряд виден в параметрах Windows, что указывает на поддержку через ПО ATK или HID-отчеты.");
            }
            return "Информация о заряде недоступна";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения состояния заряда (детали): {ex.Message}");
            return "Ошибка получения состояния заряда";
        }
    }
}