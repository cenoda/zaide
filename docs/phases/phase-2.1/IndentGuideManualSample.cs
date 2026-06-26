namespace Zaide.ManualSamples;

public class IndentGuideManualSample
{
    public void SpacesOnly()
    {
        if (true)
        {
            for (var i = 0; i < 2; i++)
            {
                if (i == 1)
                {
                    System.Console.WriteLine("spaces-only");
                }
            }
        }
    }

	public void TabsOnly()
	{
		if (true)
		{
			for (var i = 0; i < 2; i++)
			{
				if (i == 1)
				{
					System.Console.WriteLine("tabs-only");
				}
			}
		}
	}

  	public void MixedTabsAndSpaces()
  	{
  		if (true)
  		{
  			for (var i = 0; i < 2; i++)
  			{
  				if (i == 1)
  				{
  					System.Console.WriteLine("mixed-tabs-and-spaces");
  				}
  			}
  		}
  	}

    public void BlankLinesAndContinuations()
    {
        if (true)
        {

            var value = "alpha"
                .Trim()
                .ToUpperInvariant()
                .Replace("A", "Z");

            if (value.Length > 0)
            {

                System.Console.WriteLine(value);
            }
        }
    }

    public void DeepNesting()
    {
        if (true)
        {
            while (false)
            {
                foreach (var item in new[] { 1, 2, 3 })
                {
                    switch (item)
                    {
                        case 1:
                            if (item > 0)
                            {
                                System.Console.WriteLine("deep");
                            }
                            break;
                    }
                }
            }
        }
    }
}
