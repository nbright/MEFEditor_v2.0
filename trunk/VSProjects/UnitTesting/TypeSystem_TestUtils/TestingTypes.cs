﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class GenericClass<T>
{
    public M GenericMethod<M>(M test)
    {
        return test;
    }
}


class NamespaceClass<N1>
{
    internal class InnerClass<C1>
    {

    }

    internal class NamespaceClass2<N2>
    {
        internal class InnerClass<C2>
        {

        }
    }

}