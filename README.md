# TimeService_TinyCLR_Example
TimeService for TinyCLR

This example shows a TimeService adaption of Eric Olsons "FixedTimeService" for NETMF to TinyCLR.

In Main() the routine SetAppTime() is called, which tries (if needed repeatedly) to resolve the url of two different timeservers.
Then the TimeService is started by calling  TimeService.Start(). If the time could be read successfully SystemTime and Rtc time are set.
The TimeService uses two event handlers to inform about its actions.
If the time could not be read successfully from the internet, the program reads the time from the Rtc if this time was set before.

There is an option to automatically include DayLightSavingTime compensation, so that DateTime.Now gives a DayLightSavingTime corrected result
This option should better not be used as DateTime.UtcNow gets also changed according to the DayLightSavingTimeOffset (which is wrong)
It is better to retrieve the DST Offset through the now implemented method 'TimeService.GetDstOffset(DateTime.Now)' and add this offset to the DateTime.Now value
