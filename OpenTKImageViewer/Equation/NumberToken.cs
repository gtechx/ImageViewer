﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTKImageViewer.Equation
{
    class NumberToken : ValueToken
    {
        private float value;

        public NumberToken(float number)
        {
            value = number;
        }

        public override string ToOpenGl()
        {
            return $"vec4(float({value}))";
        }
    }
}
